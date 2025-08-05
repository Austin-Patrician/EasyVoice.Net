import { REALTIME_CONFIG, REALTIME_ERROR_MESSAGES } from '../constants';
import { AudioVisualizationData } from '../types';

export interface AudioPermissions {
  microphone: boolean;
  speaker: boolean;
}

export interface AudioDeviceInfo {
  deviceId: string;
  label: string;
  kind: MediaDeviceKind;
}

export class AudioService {
  private mediaStream: MediaStream | null = null;
  private mediaRecorder: MediaRecorder | null = null;
  private audioContext: AudioContext | null = null;
  private analyser: AnalyserNode | null = null;
  private microphone: MediaStreamAudioSourceNode | null = null;
  private outputAudioContext: AudioContext | null = null;
  private isRecording: boolean = false;
  private isPlaying: boolean = false;
  private recordingChunks: Blob[] = [];
  private eventListeners: Map<string, Function[]> = new Map();
  private visualizationData: AudioVisualizationData = {
    frequencies: [],
    volume: 0,
    isRecording: false,
    isPlaying: false
  };
  private frequencyDataBuffer: Uint8Array = new Uint8Array(0);
  private animationFrameId: number | null = null;

  constructor() {
    this.initializeEventListeners();
  }

  private initializeEventListeners(): void {
    this.eventListeners.set('permissionGranted', []);
    this.eventListeners.set('permissionDenied', []);
    this.eventListeners.set('recordingStarted', []);
    this.eventListeners.set('recordingStopped', []);
    this.eventListeners.set('audioData', []);
    this.eventListeners.set('visualizationUpdate', []);
    this.eventListeners.set('error', []);
    this.eventListeners.set('deviceChanged', []);
  }

  public addEventListener(event: string, callback: Function): void {
    const listeners = this.eventListeners.get(event) || [];
    listeners.push(callback);
    this.eventListeners.set(event, listeners);
  }

  public removeEventListener(event: string, callback: Function): void {
    const listeners = this.eventListeners.get(event) || [];
    const index = listeners.indexOf(callback);
    if (index > -1) {
      listeners.splice(index, 1);
    }
  }

  private emit(event: string, data?: any): void {
    const listeners = this.eventListeners.get(event) || [];
    listeners.forEach(callback => {
      try {
        callback(data);
      } catch (error) {
        console.error(`Error in audio event listener for ${event}:`, error);
      }
    });
  }

  public async requestPermissions(): Promise<AudioPermissions> {
    try {
      // 请求麦克风权限
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: REALTIME_CONFIG.INPUT_SAMPLE_RATE,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });

      // 检查扬声器权限（通过创建AudioContext）
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
      await audioContext.resume();
      audioContext.close();

      // 暂时停止流，稍后会重新获取
      stream.getTracks().forEach(track => track.stop());

      const permissions: AudioPermissions = {
        microphone: true,
        speaker: true
      };

      this.emit('permissionGranted', permissions);
      return permissions;

    } catch (error) {
      console.error('音频权限请求失败:', error);
      const permissions: AudioPermissions = {
        microphone: false,
        speaker: false
      };
      
      this.emit('permissionDenied', error);
      throw new Error(REALTIME_ERROR_MESSAGES.AUDIO_PERMISSION_DENIED);
    }
  }

  public async getAudioDevices(): Promise<AudioDeviceInfo[]> {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      return devices
        .filter(device => device.kind === 'audioinput' || device.kind === 'audiooutput')
        .map(device => ({
          deviceId: device.deviceId,
          label: device.label || `${device.kind === 'audioinput' ? '麦克风' : '扬声器'} ${device.deviceId.slice(0, 8)}`,
          kind: device.kind
        }));
    } catch (error) {
      console.error('获取音频设备失败:', error);
      throw new Error(REALTIME_ERROR_MESSAGES.AUDIO_DEVICE_ERROR);
    }
  }

  public async initializeAudioContext(deviceId?: string): Promise<void> {
    try {
      // 创建音频上下文
      this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)({
        sampleRate: REALTIME_CONFIG.INPUT_SAMPLE_RATE
      });

      // 获取媒体流
      const constraints: MediaStreamConstraints = {
        audio: {
          deviceId: deviceId ? { exact: deviceId } : undefined,
          sampleRate: REALTIME_CONFIG.INPUT_SAMPLE_RATE,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      };

      this.mediaStream = await navigator.mediaDevices.getUserMedia(constraints);

      // 创建音频分析器
      this.analyser = this.audioContext.createAnalyser();
      this.analyser.fftSize = REALTIME_CONFIG.VISUALIZATION.FFT_SIZE;
      this.analyser.smoothingTimeConstant = REALTIME_CONFIG.VISUALIZATION.SMOOTHING_TIME_CONSTANT;
      this.analyser.minDecibels = REALTIME_CONFIG.VISUALIZATION.MIN_DECIBELS;
      this.analyser.maxDecibels = REALTIME_CONFIG.VISUALIZATION.MAX_DECIBELS;

      // 连接麦克风到分析器
      this.microphone = this.audioContext.createMediaStreamSource(this.mediaStream);
      this.microphone.connect(this.analyser);

      // 初始化可视化数据
      const bufferLength = this.analyser.frequencyBinCount;
      this.frequencyDataBuffer = new Uint8Array(bufferLength);
        this.visualizationData = {
          frequencies: Array.from(this.frequencyDataBuffer),
          volume: 0,
          isRecording: true,
          isPlaying: false
        };

      // 开始可视化更新
      this.startVisualization();

    } catch (error) {
      console.error('初始化音频上下文失败:', error);
      throw new Error(REALTIME_ERROR_MESSAGES.AUDIO_DEVICE_ERROR);
    }
  }

  private startVisualization(): void {
    if (!this.analyser) return;

    const updateVisualization = () => {
      if (!this.analyser || (!this.visualizationData.isRecording && !this.visualizationData.isPlaying)) return;

      // 获取频域数据
      this.analyser.getByteFrequencyData(this.frequencyDataBuffer);
      this.visualizationData.frequencies = Array.from(this.frequencyDataBuffer);

      // 计算音量
      let sum = 0;
      for (let i = 0; i < this.visualizationData.frequencies.length; i++) {
          sum += this.visualizationData.frequencies[i];
        }
      this.visualizationData.volume = sum / this.visualizationData.frequencies.length;

      // 发送可视化数据
      this.emit('visualizationUpdate', { ...this.visualizationData });

      // 继续下一帧
      this.animationFrameId = requestAnimationFrame(updateVisualization);
    };

    updateVisualization();
  }

  public async startRecording(): Promise<void> {
    if (!this.mediaStream) {
      throw new Error('音频流未初始化');
    }

    if (this.isRecording) {
      return;
    }

    try {
      // 创建MediaRecorder
      const options: MediaRecorderOptions = {
        mimeType: 'audio/webm;codecs=opus',
        audioBitsPerSecond: 16000
      };

      this.mediaRecorder = new MediaRecorder(this.mediaStream, options);
      this.recordingChunks = [];

      this.mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.recordingChunks.push(event.data);
          // 实时发送音频数据
          this.emit('audioData', event.data);
        }
      };

      this.mediaRecorder.onstop = () => {
        this.isRecording = false;
        this.emit('recordingStopped', this.recordingChunks);
      };

      this.mediaRecorder.onerror = (event) => {
        console.error('录音错误:', event);
        this.emit('error', new Error('录音过程中发生错误'));
      };

      // 开始录音，每100ms发送一次数据
      this.mediaRecorder.start(100);
      this.isRecording = true;
      this.emit('recordingStarted');

    } catch (error) {
      console.error('开始录音失败:', error);
      throw new Error(REALTIME_ERROR_MESSAGES.AUDIO_DEVICE_ERROR);
    }
  }

  public stopRecording(): void {
    if (this.mediaRecorder && this.isRecording) {
      this.mediaRecorder.stop();
    }
  }

  public async playAudioData(audioData: ArrayBuffer): Promise<void> {
    try {
      // 创建输出音频上下文
      if (!this.outputAudioContext) {
        this.outputAudioContext = new (window.AudioContext || (window as any).webkitAudioContext)({
          sampleRate: REALTIME_CONFIG.OUTPUT_SAMPLE_RATE
        });
      }

      // 解码音频数据
      const audioBuffer = await this.outputAudioContext.decodeAudioData(audioData.slice(0));
      
      // 创建音频源
      const source = this.outputAudioContext.createBufferSource();
      source.buffer = audioBuffer;
      source.connect(this.outputAudioContext.destination);
      
      // 播放音频
      source.start();
      this.isPlaying = true;
      
      source.onended = () => {
        this.isPlaying = false;
      };

    } catch (error) {
      console.error('播放音频失败:', error);
      throw new Error('音频播放失败');
    }
  }

  public async convertBlobToArrayBuffer(blob: Blob): Promise<ArrayBuffer> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        if (reader.result instanceof ArrayBuffer) {
          resolve(reader.result);
        } else {
          reject(new Error('转换失败'));
        }
      };
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(blob);
    });
  }

  public async convertAudioToPCM(audioData: ArrayBuffer): Promise<ArrayBuffer> {
    try {
      if (!this.audioContext) {
        throw new Error('音频上下文未初始化');
      }

      const audioBuffer = await this.audioContext.decodeAudioData(audioData.slice(0));
      const pcmData = audioBuffer.getChannelData(0); // 获取第一个声道
      
      // 转换为16位PCM
      const pcm16 = new Int16Array(pcmData.length);
      for (let i = 0; i < pcmData.length; i++) {
        pcm16[i] = Math.max(-32768, Math.min(32767, pcmData[i] * 32768));
      }
      
      return pcm16.buffer;
    } catch (error) {
      console.error('转换PCM失败:', error);
      throw error;
    }
  }

  public getVisualizationData(): AudioVisualizationData {
    return { ...this.visualizationData };
  }

  public isCurrentlyRecording(): boolean {
    return this.isRecording;
  }

  public isCurrentlyPlaying(): boolean {
    return this.isPlaying;
  }

  public async changeAudioDevice(deviceId: string): Promise<void> {
    // 停止当前录音
    if (this.isRecording) {
      this.stopRecording();
    }

    // 清理当前音频上下文
    this.cleanup();

    // 重新初始化新设备
    await this.initializeAudioContext(deviceId);
    this.emit('deviceChanged', deviceId);
  }

  public setVolume(volume: number): void {
    if (this.outputAudioContext) {
      const gainNode = this.outputAudioContext.createGain();
      gainNode.gain.value = Math.max(0, Math.min(1, volume));
    }
  }

  public cleanup(): void {
    // 停止录音
    if (this.isRecording) {
      this.stopRecording();
    }

    // 停止可视化
    this.visualizationData.isRecording = false;
    this.visualizationData.isPlaying = false;
    if (this.animationFrameId) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }

    // 关闭媒体流
    if (this.mediaStream) {
      this.mediaStream.getTracks().forEach(track => track.stop());
      this.mediaStream = null;
    }

    // 关闭音频上下文
    if (this.audioContext && this.audioContext.state !== 'closed') {
      this.audioContext.close();
      this.audioContext = null;
    }

    if (this.outputAudioContext && this.outputAudioContext.state !== 'closed') {
      this.outputAudioContext.close();
      this.outputAudioContext = null;
    }

    // 清理其他资源
    this.mediaRecorder = null;
    this.analyser = null;
    this.microphone = null;
    this.recordingChunks = [];
  }

  public destroy(): void {
    this.cleanup();
    this.eventListeners.clear();
  }
}

// 创建默认实例
export const audioService = new AudioService();