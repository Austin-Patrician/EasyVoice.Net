/**
 * 音频管理器
 * 负责音频录制、播放和可视化
 */
export class AudioManager {
  private mediaRecorder: MediaRecorder | null = null;
  private audioContext: AudioContext | null = null;
  private analyser: AnalyserNode | null = null;
  private microphone: MediaStreamAudioSourceNode | null = null;
  private audioChunks: Blob[] = [];
  private isRecording = false;
  private isPlaying = false;
  private stream: MediaStream | null = null;
  private audioElement: HTMLAudioElement | null = null;
  private visualizationCallback: ((data: Uint8Array) => void) | null = null;
  private animationId: number | null = null;

  // 音频配置
  private readonly sampleRate = 24000;
  private readonly channels = 1;
  private readonly bufferSize = 4096;

  constructor() {
    this.initializeAudioContext();
  }

  // #region Initialization

  /**
   * 初始化音频上下文
   */
  private async initializeAudioContext(): Promise<void> {
    try {
      this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)({
        sampleRate: this.sampleRate,
      });

      // 创建分析器节点用于可视化
      this.analyser = this.audioContext.createAnalyser();
      this.analyser.fftSize = 256;
      this.analyser.smoothingTimeConstant = 0.8;
    } catch (error) {
      console.error('初始化音频上下文失败:', error);
      throw new Error('音频上下文初始化失败');
    }
  }

  /**
   * 请求音频权限
   */
  public async requestPermissions(): Promise<{ microphone: boolean; speaker: boolean }> {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ 
        audio: {
          sampleRate: this.sampleRate,
          channelCount: this.channels,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        } 
      });
      
      // 立即停止流，只是为了检查权限
      stream.getTracks().forEach(track => track.stop());
      
      return {
        microphone: true,
        speaker: true // 浏览器通常不需要单独的扬声器权限
      };
    } catch (error) {
      console.error('请求音频权限失败:', error);
      return {
        microphone: false,
        speaker: true
      };
    }
  }

  /**
   * 获取音频设备列表
   */
  public async getAudioDevices(): Promise<{
    microphones: MediaDeviceInfo[];
    speakers: MediaDeviceInfo[];
  }> {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      
      return {
        microphones: devices.filter(device => device.kind === 'audioinput'),
        speakers: devices.filter(device => device.kind === 'audiooutput')
      };
    } catch (error) {
      console.error('获取音频设备失败:', error);
      return {
        microphones: [],
        speakers: []
      };
    }
  }

  // #endregion

  // #region Recording

  /**
   * 开始录音
   */
  public async startRecording(onDataAvailable?: (audioData: ArrayBuffer) => void): Promise<void> {
    try {
      if (this.isRecording) {
        console.warn('录音已在进行中');
        return;
      }

      // 获取音频流
      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: this.sampleRate,
          channelCount: this.channels,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });

      // 恢复音频上下文（如果被暂停）
      if (this.audioContext?.state === 'suspended') {
        await this.audioContext.resume();
      }

      // 连接到分析器用于可视化
      if (this.audioContext && this.analyser) {
        this.microphone = this.audioContext.createMediaStreamSource(this.stream);
        this.microphone.connect(this.analyser);
        this.startVisualization();
      }

      // 创建MediaRecorder
      const options: MediaRecorderOptions = {
        mimeType: 'audio/webm;codecs=opus',
        audioBitsPerSecond: 128000
      };

      // 检查浏览器支持的格式
      if (!MediaRecorder.isTypeSupported(options.mimeType!)) {
        options.mimeType = 'audio/webm';
        if (!MediaRecorder.isTypeSupported(options.mimeType)) {
          options.mimeType = 'audio/mp4';
        }
      }

      this.mediaRecorder = new MediaRecorder(this.stream, options);
      this.audioChunks = [];

      this.mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.audioChunks.push(event.data);
          
          // 如果提供了回调，转换为ArrayBuffer并调用
          if (onDataAvailable) {
            event.data.arrayBuffer().then(arrayBuffer => {
              onDataAvailable(arrayBuffer);
            }).catch(error => {
              console.error('转换音频数据失败:', error);
            });
          }
        }
      };

      this.mediaRecorder.onerror = (event) => {
        console.error('录音错误:', event);
        this.stopRecording();
      };

      // 开始录音，每100ms产生一个数据块
      this.mediaRecorder.start(100);
      this.isRecording = true;

      console.log('录音已开始');
    } catch (error) {
      console.error('开始录音失败:', error);
      throw new Error('开始录音失败');
    }
  }

  /**
   * 停止录音
   */
  public stopRecording(): Promise<Blob | null> {
    return new Promise((resolve) => {
      if (!this.isRecording || !this.mediaRecorder) {
        resolve(null);
        return;
      }

      this.mediaRecorder.onstop = () => {
        const audioBlob = this.audioChunks.length > 0 
          ? new Blob(this.audioChunks, { type: this.mediaRecorder?.mimeType || 'audio/webm' })
          : null;
        
        this.cleanup();
        resolve(audioBlob);
      };

      this.mediaRecorder.stop();
      this.isRecording = false;

      console.log('录音已停止');
    });
  }

  // #endregion

  // #region Playback

  /**
   * 播放音频
   */
  public async playAudio(audioData: ArrayBuffer | Blob | string): Promise<void> {
    try {
      if (this.isPlaying) {
        this.stopPlayback();
      }

      this.audioElement = new Audio();
      
      if (typeof audioData === 'string') {
        // URL字符串
        this.audioElement.src = audioData;
      } else if (audioData instanceof Blob) {
        // Blob对象
        this.audioElement.src = URL.createObjectURL(audioData);
      } else {
        // ArrayBuffer
        const blob = new Blob([audioData], { type: 'audio/webm' });
        this.audioElement.src = URL.createObjectURL(blob);
      }

      this.audioElement.onended = () => {
        this.isPlaying = false;
        this.stopVisualization();
      };

      this.audioElement.onerror = (error) => {
        console.error('音频播放错误:', error);
        this.isPlaying = false;
        this.stopVisualization();
      };

      await this.audioElement.play();
      this.isPlaying = true;

      // 如果有音频上下文，连接到分析器用于可视化
      if (this.audioContext && this.analyser && this.audioElement) {
        const source = this.audioContext.createMediaElementSource(this.audioElement);
        source.connect(this.analyser);
        source.connect(this.audioContext.destination);
        this.startVisualization();
      }

      console.log('音频播放已开始');
    } catch (error) {
      console.error('播放音频失败:', error);
      throw new Error('播放音频失败');
    }
  }

  /**
   * 停止播放
   */
  public stopPlayback(): void {
    if (this.audioElement) {
      this.audioElement.pause();
      this.audioElement.currentTime = 0;
      
      // 清理URL对象
      if (this.audioElement.src.startsWith('blob:')) {
        URL.revokeObjectURL(this.audioElement.src);
      }
      
      this.audioElement = null;
    }
    
    this.isPlaying = false;
    this.stopVisualization();
    
    console.log('音频播放已停止');
  }

  /**
   * 设置播放音量
   */
  public setVolume(volume: number): void {
    if (this.audioElement) {
      this.audioElement.volume = Math.max(0, Math.min(1, volume));
    }
  }

  /**
   * 获取播放音量
   */
  public getVolume(): number {
    return this.audioElement?.volume || 0;
  }

  // #endregion

  // #region Visualization

  /**
   * 设置可视化回调
   */
  public setVisualizationCallback(callback: (data: Uint8Array) => void): void {
    this.visualizationCallback = callback;
  }

  /**
   * 开始可视化
   */
  private startVisualization(): void {
    if (!this.analyser || !this.visualizationCallback) {
      return;
    }

    const bufferLength = this.analyser.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    const updateVisualization = () => {
      if (!this.analyser || !this.visualizationCallback) {
        return;
      }

      this.analyser.getByteFrequencyData(dataArray);
      this.visualizationCallback(dataArray);
      
      this.animationId = requestAnimationFrame(updateVisualization);
    };

    updateVisualization();
  }

  /**
   * 停止可视化
   */
  private stopVisualization(): void {
    if (this.animationId) {
      cancelAnimationFrame(this.animationId);
      this.animationId = null;
    }
  }

  /**
   * 获取音频级别（用于音量指示器）
   */
  public getAudioLevel(): number {
    if (!this.analyser) {
      return 0;
    }

    const bufferLength = this.analyser.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);
    this.analyser.getByteFrequencyData(dataArray);

    // 计算平均音量
    let sum = 0;
    for (let i = 0; i < bufferLength; i++) {
      sum += dataArray[i];
    }
    
    return sum / bufferLength / 255; // 归一化到0-1
  }

  // #endregion

  // #region Status

  /**
   * 检查是否正在录音
   */
  public isRecordingActive(): boolean {
    return this.isRecording;
  }

  /**
   * 检查是否正在播放
   */
  public isPlayingActive(): boolean {
    return this.isPlaying;
  }

  /**
   * 获取音频上下文状态
   */
  public getAudioContextState(): AudioContextState | null {
    return this.audioContext?.state || null;
  }

  // #endregion

  // #region Cleanup

  /**
   * 清理录音资源
   */
  private cleanup(): void {
    // 停止所有音轨
    if (this.stream) {
      this.stream.getTracks().forEach(track => track.stop());
      this.stream = null;
    }

    // 断开麦克风连接
    if (this.microphone) {
      this.microphone.disconnect();
      this.microphone = null;
    }

    // 停止可视化
    this.stopVisualization();

    // 清理MediaRecorder
    this.mediaRecorder = null;
    this.audioChunks = [];
  }

  /**
   * 销毁音频管理器
   */
  public dispose(): void {
    this.stopRecording();
    this.stopPlayback();
    this.cleanup();

    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
    }

    this.analyser = null;
    this.visualizationCallback = null;
  }

  // #endregion
}

// 创建默认实例
export const audioManager = new AudioManager();