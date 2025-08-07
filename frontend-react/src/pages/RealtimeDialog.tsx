import React, { useState, useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Button, Card, Input, Badge, Modal, Slider } from 'antd';
import { Divider } from 'antd';
const { TextArea } = Input;
import { Mic, MicOff, Play, Square, Send, Settings, Phone, PhoneOff, User, Volume2 } from 'lucide-react';
import { toast } from 'sonner';

interface AudioConfig {
  channels: number;
  format: string;
  sampleRate: number;
  bitDepth: number;
  chunkSize: number;
}

interface SessionConfig {
  appId: string;
  accessKey: string;
  audioConfig: AudioConfig;
  speaker?: string;
  botName?: string;
  systemRole?: string;
  speakingStyle?: string;
  cluster?: string;
  voiceType?: string;
  audioEncoding?: string;
  sampleRate?: number;
  enableServerVad?: boolean;
}

interface DialogEvent {
  id: string;
  type: string;
  timestamp: string;
  data: any;
}

interface AudioQueueItem {
  id: string;
  audioData: number[];
  timestamp: number;
}

// 通话状态枚举
enum CallState {
  IDLE = 'idle',           // 待机
  CONNECTING = 'connecting', // 连接中
  DIALING = 'dialing',     // 拨打中
  CALLING = 'calling',     // 通话中
  ENDING = 'ending',       // 挂断中
  ERROR = 'error'          // 错误状态
}

interface DialogState {
  isUserQuerying: boolean;
  isSendingChatTts: boolean;
  isAiSpeaking: boolean;
  isConnected: boolean;
  isRecording: boolean;
  callState: CallState;
  callStartTime: number | null;
  audioLevel: number; // 音频音量级别 0-100
}

const RealtimeDialog: React.FC = () => {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [sessionId, setSessionId] = useState<string>('');
  const [isRecording, setIsRecording] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [events, setEvents] = useState<DialogEvent[]>([]);
  const [chatText, setChatText] = useState('');
  
  // 对话状态管理
  const [dialogState, setDialogState] = useState<DialogState>({
    isUserQuerying: false,
    isSendingChatTts: false,
    isAiSpeaking: false,
    isConnected: false,
    isRecording: false,
    callState: CallState.IDLE,
    callStartTime: null,
    audioLevel: 0
  });
  
  // 设置弹窗状态
  const [showSettings, setShowSettings] = useState(false);
  
  // 通话时长
  const [callDuration, setCallDuration] = useState(0);
  const callTimerRef = useRef<NodeJS.Timeout | null>(null);
  
  // 音频队列管理
  const [audioQueue, setAudioQueue] = useState<AudioQueueItem[]>([]);
  const audioQueueRef = useRef<AudioQueueItem[]>([]);
  const isPlayingAudioRef = useRef<boolean>(false);
  const currentAudioSourceRef = useRef<AudioBufferSourceNode | null>(null);
  
  // 配置状态
  const [sessionConfig, setSessionConfig] = useState<SessionConfig>({
    appId: '',
    accessKey: '',
    audioConfig: {
      channels: 1,
      format: 'pcm',
      sampleRate: 16000,
      bitDepth: 16,
      chunkSize: 4096
    },
    cluster: 'volcengine_streaming_common',
    voiceType: 'zh_female_1',
    audioEncoding: 'wav',
    sampleRate: 16000,
    enableServerVad: true
  });
  
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const audioContextRef = useRef<AudioContext | null>(null);
  const audioStreamRef = useRef<MediaStream | null>(null);
  const audioWorkletRef = useRef<AudioWorkletNode | null>(null);
  const continuousRecordingRef = useRef<boolean>(false);
  
  // 初始化SignalR连接
  useEffect(() => {
    const initializeConnection = async () => {
      try {
        const newConnection = new HubConnectionBuilder()
          .withUrl('http://localhost:5094/realtimeDialogHub')
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Information)
          .build();
        
        console.log('SignalR连接对象已创建');
        setConnection(newConnection);
        
        // 设置连接状态事件监听
        newConnection.onreconnecting(() => {
          console.log('SignalR正在重新连接...');
          setDialogState(prev => ({ ...prev, isConnected: false }));
          toast.info('正在重新连接...');
        });
        
        newConnection.onreconnected(() => {
          console.log('SignalR重新连接成功');
          setDialogState(prev => ({ ...prev, isConnected: true }));
          toast.success('重新连接成功');
        });
        
        newConnection.onclose(() => {
          console.log('SignalR连接已关闭');
          setDialogState(prev => ({ ...prev, isConnected: false }));
          toast.error('连接已断开');
        });
        
      } catch (error) {
        console.error('初始化SignalR连接失败:', error);
        toast.error('初始化连接失败');
      }
    };
    
    initializeConnection();
    
    return () => {
      if (connection) {
        console.log('清理SignalR连接');
        connection.stop();
      }
    };
  }, []);
  
  // 设置SignalR事件监听
  useEffect(() => {
    if (connection) {
      console.log('设置SignalR事件监听');
      
      // 对话事件监听
      connection.on('OnDialogEvent', (eventType, data) => {
        addEvent(eventType, data);
        
        switch(eventType) {
          case 'ASR_INFO':
            toast.info('检测到语音输入');
            break;
          case 'ASR_RESPONSE':
            toast.success(`识别结果: ${data.text || ''}`);
            break;
          case 'ASR_ENDED':
            toast.info('语音识别结束');
            break;
          case 'TTS_RESPONSE':
            toast.info('TTS响应');
            setDialogState(prev => ({ ...prev, isAiSpeaking: true }));
            break;
          case 'TTS_ENDED':
            toast.info('TTS结束');
            setDialogState(prev => ({ ...prev, isAiSpeaking: false }));
            break;
          case 'SERVER_FULL_RESPONSE':
            handleServerFullResponse(data);
            break;
        }
      });
      
      // 处理服务器完整响应（包含事件代码）
      const handleServerFullResponse = (response: any) => {
        const event = response?.event;
        const payloadMsg = response?.payload_msg || {};
        
        if (event === 450) {
          // 用户开始说话，清空音频队列（打断机制）
          console.log('用户开始说话，清空音频队列');
          clearAudioQueue();
          setDialogState(prev => ({ 
            ...prev, 
            isUserQuerying: true,
            isAiSpeaking: false 
          }));
          toast.info('检测到用户说话，已打断AI回复');
        }
        
        if (event === 350 && dialogState.isSendingChatTts && payloadMsg?.tts_type === 'chat_tts_text') {
          // ChatTTS文本处理完成
          clearAudioQueue();
          setDialogState(prev => ({ ...prev, isSendingChatTts: false }));
        }
        
        if (event === 459) {
          // 用户说话结束
          setDialogState(prev => ({ ...prev, isUserQuerying: false }));
        }
      };
      
      connection.on('OnAudioDataReceived', (audioData) => {
        addEvent('AUDIO_DATA', { length: audioData.length });
        // 如果不是在发送ChatTTS文本，则将音频加入队列
        if (!dialogState.isSendingChatTts) {
          enqueueAudioData(audioData);
        }
      });
      
      connection.on('OnSessionStarted', (sessionId) => {
        addEvent('SESSION_STARTED', { sessionId });
        setSessionId(sessionId);
        toast.success('会话已开始');
      });
      
      connection.on('OnSessionEnded', (sessionId) => {
        addEvent('SESSION_ENDED', { sessionId });
        setSessionId('');
        toast.info('会话已结束');
      });
      
      connection.on('OnError', (error) => {
        addEvent('ERROR', { error });
        toast.error(`错误: ${error}`);
      });
      
      // 音频事件监听
      connection.on('OnRecordingStarted', (deviceId) => {
        setIsRecording(true);
        toast.success('开始录制');
      });
      
      connection.on('OnRecordingStopped', (deviceId) => {
        setIsRecording(false);
        toast.info('停止录制');
      });
      
      connection.on('OnPlaybackStarted', (deviceId) => {
        setIsPlaying(true);
      });
      
      connection.on('OnPlaybackStopped', (deviceId) => {
        setIsPlaying(false);
      });
      
      connection.on('OnAudioError', (error) => {
        toast.error(`音频错误: ${error}`);
      });
    }
  }, [connection]);
  
  // 添加事件到列表
  const addEvent = (type: string, data: any) => {
    const event: DialogEvent = {
      id: Date.now().toString(),
      type,
      timestamp: new Date().toLocaleTimeString(),
      data
    };
    setEvents(prev => [event, ...prev].slice(0, 100)); // 保留最近100个事件
  };
  
  // 音频队列管理函数
  const enqueueAudioData = (audioData: number[]) => {
    const audioItem: AudioQueueItem = {
      id: Date.now().toString() + Math.random(),
      audioData,
      timestamp: Date.now()
    };
    
    audioQueueRef.current.push(audioItem);
    setAudioQueue(prev => [...prev, audioItem]);
    
    // 如果当前没有在播放音频，开始播放
    if (!isPlayingAudioRef.current) {
      processAudioQueue();
    }
  };
  
  const clearAudioQueue = () => {
    // 停止当前播放的音频
    if (currentAudioSourceRef.current) {
      try {
        currentAudioSourceRef.current.stop();
        currentAudioSourceRef.current.disconnect();
      } catch (e) {
        console.warn('停止音频播放时出错:', e);
      }
      currentAudioSourceRef.current = null;
    }
    
    // 清空队列
    audioQueueRef.current = [];
    setAudioQueue([]);
    isPlayingAudioRef.current = false;
  };
  
  const processAudioQueue = async () => {
    if (isPlayingAudioRef.current || audioQueueRef.current.length === 0) {
      return;
    }
    
    isPlayingAudioRef.current = true;
    
    while (audioQueueRef.current.length > 0) {
      const audioItem = audioQueueRef.current.shift();
      if (audioItem) {
        setAudioQueue(prev => prev.filter(item => item.id !== audioItem.id));
        await playAudioDataContinuous(audioItem.audioData);
        
        // 检查是否被打断
        if (dialogState.isUserQuerying) {
          break;
        }
      }
    }
    
    isPlayingAudioRef.current = false;
  };
  
  const playAudioDataContinuous = async (audioData: number[]): Promise<void> => {
    return new Promise((resolve, reject) => {
      try {
        const audioContext = new AudioContext();
        const audioBuffer = audioContext.createBuffer(1, audioData.length, sessionConfig.audioConfig.sampleRate);
        const channelData = audioBuffer.getChannelData(0);
        
        // 转换音频数据
        for (let i = 0; i < audioData.length; i++) {
          channelData[i] = audioData[i] / 32768; // 假设是16位PCM
        }
        
        const source = audioContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(audioContext.destination);
        
        currentAudioSourceRef.current = source;
        
        source.onended = () => {
          currentAudioSourceRef.current = null;
          resolve();
        };
        
        source.start();
      } catch (error) {
        console.error('播放音频失败:', error);
        reject(error);
      }
    });
  };
  
  // 格式化通话时长
  const formatCallDuration = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };
  
  // 音频可视化动画
  const updateAudioVisualization = (level: number) => {
    setDialogState(prev => ({ ...prev, audioLevel: level }));
  };
  
  // 开始通话
  const startCall = async () => {
    console.log('connection对象:', connection);
    
    if (!sessionConfig.appId || !sessionConfig.accessKey) {
      toast.error('请先配置App ID和Access Key');
      setShowSettings(true);
      return;
    }
    
    // 检查connection对象是否存在
    if (!connection) {
      toast.error('SignalR连接未初始化，请刷新页面重试');
      return;
    }

    setDialogState(prev => ({ ...prev, callState: CallState.CONNECTING }));
    toast.info('开始建立连接...');
    
    try {
      await connectToHub();
      setDialogState(prev => ({ ...prev, callState: CallState.DIALING }));
      toast.info('连接成功，正在创建会话...');
      
      // 创建会话
      await createSession();
      
      setDialogState(prev => ({ 
        ...prev, 
        callState: CallState.CALLING,
        callStartTime: Date.now()
      }));
      
      toast.success('通话已建立');
    } catch (error) {
      console.error('Start call error:', error);
      setDialogState(prev => ({ ...prev, callState: CallState.IDLE }));
      toast.error(`通话建立失败: ${error.message || error}`);
    }
  };
  
  // 结束通话
  const endCall = async () => {
    setDialogState(prev => ({ ...prev, callState: CallState.ENDING }));
    
    try {
      if (dialogState.isRecording) {
        await stopBrowserRecording();
      }
      
      await endSession();
      await disconnectFromHub();
      
      setDialogState(prev => ({ 
        ...prev, 
        callState: CallState.IDLE,
        callStartTime: null,
        isRecording: false,
        audioLevel: 0
      }));
      
      toast.success('通话已结束');
    } catch (error) {
      console.error('End call error:', error);
      setDialogState(prev => ({ ...prev, callState: CallState.IDLE }));
      toast.error('结束通话时出现错误');
    }
  };

  // 连接到SignalR Hub
  const connectToHub = async () => {
    if (!connection) {
      throw new Error('SignalR连接对象未初始化');
    }
    
    // 检查连接状态
    if (connection.state === 'Connected') {
      console.log('SignalR已连接，跳过重复连接');
      setDialogState(prev => ({ ...prev, isConnected: true }));
      return;
    }
    
    try {
      console.log('开始连接SignalR Hub...');
      await connection.start();
      console.log('SignalR连接成功');
      setDialogState(prev => ({ ...prev, isConnected: true }));
      toast.success('连接成功');
    } catch (error) {
      console.error('连接失败:', error);
      setDialogState(prev => ({ ...prev, callState: CallState.IDLE }));
      toast.error(`连接失败: ${error.message || error}`);
      throw error;
    }
  };
  
  // 断开连接
  const disconnectFromHub = async () => {
    if (connection) {
      try {
        await connection.stop();
        setIsConnected(false);
        toast.info('已断开连接');
      } catch (error) {
        console.error('断开连接失败:', error);
      }
    }
  };
  
  // 创建会话
  const createSession = async () => {
    console.log('createSession函数被调用');
    console.log('连接状态:', connection?.state);
    console.log('isConnected状态:', dialogState.isConnected);
    
    if (!connection) {
      toast.error('SignalR连接对象不存在');
      throw new Error('SignalR连接对象不存在');
    }
    
    if (connection.state !== 'Connected') {
      toast.error('请先连接到服务器');
      throw new Error('SignalR未连接');
    }
    
    if (!sessionConfig.appId || !sessionConfig.accessKey) {
      toast.error('请填写AppId和AccessKey');
      throw new Error('配置信息不完整');
    }
    
    try {
      console.log('正在调用CreateSession，配置:', sessionConfig);
      const newSessionId = await connection.invoke('CreateSession', sessionConfig);
      console.log('CreateSession返回结果:', newSessionId);
      
      if (newSessionId) {
        setSessionId(newSessionId);
        toast.success(`会话创建成功: ${newSessionId}`);
        
        // 自动开始录音
        setTimeout(async () => {
          try {
            await startBrowserRecording();
            toast.success('已开始录音，可以开始对话了');
          } catch (error) {
            console.error('自动开始录音失败:', error);
            toast.warning('会话创建成功，但录音启动失败，请手动点击麦克风按钮');
          }
        }, 1000);
      } else {
        toast.error('会话创建失败：返回的会话ID为空');
        throw new Error('会话ID为空');
      }
    } catch (error) {
      console.error('创建会话失败:', error);
      setDialogState(prev => ({ ...prev, callState: CallState.IDLE }));
      toast.error(`创建会话失败: ${error.message || error}`);
      throw error;
    }
  };
  
  // 结束会话
  const endSession = async () => {
    if (!connection || !sessionId) {
      return;
    }
    
    try {
      await connection.invoke('EndSession', sessionId);
      setSessionId('');
      toast.info('会话已结束');
    } catch (error) {
      console.error('结束会话失败:', error);
      toast.error('结束会话失败');
    }
  };
  
  // 开始录制
  const startRecording = async () => {
    if (!connection || !isConnected) {
      toast.error('请先连接到服务器');
      return;
    }
    
    try {
      // 设置音频配置
      await connection.invoke('SetAudioConfig', sessionConfig.audioConfig);
      
      // 开始录制
      const success = await connection.invoke('StartRecording');
      if (success) {
        // 同时启动浏览器录制
        await startBrowserRecording();
      }
    } catch (error) {
      console.error('开始录制失败:', error);
      toast.error('开始录制失败');
    }
  };
  
  // 停止录制
  const stopRecording = async () => {
    if (!connection) {
      return;
    }
    
    try {
      await connection.invoke('StopRecording');
      stopBrowserRecording();
    } catch (error) {
      console.error('停止录制失败:', error);
      toast.error('停止录制失败');
    }
  };
  
  // 连续音频流录制（类似Python版本）
  const startBrowserRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ 
        audio: {
          sampleRate: sessionConfig.audioConfig.sampleRate,
          channelCount: sessionConfig.audioConfig.channels,
          echoCancellation: true,
          noiseSuppression: true
        } 
      });
      
      audioStreamRef.current = stream;
      audioContextRef.current = new AudioContext({ sampleRate: sessionConfig.audioConfig.sampleRate });
      
      const source = audioContextRef.current.createMediaStreamSource(stream);
      
      // 创建ScriptProcessorNode进行连续音频处理
      const processor = audioContextRef.current.createScriptProcessor(sessionConfig.audioConfig.chunkSize, 1, 1);
      
      processor.onaudioprocess = (event) => {
        if (!continuousRecordingRef.current) return;
        
        const inputBuffer = event.inputBuffer;
        const inputData = inputBuffer.getChannelData(0);
        
        // 转换为16位PCM格式
        const pcmData = new Int16Array(inputData.length);
        for (let i = 0; i < inputData.length; i++) {
          pcmData[i] = Math.max(-32768, Math.min(32767, inputData[i] * 32768));
        }
        
        // 连续发送音频数据
        sendContinuousAudioData(Array.from(pcmData));
      };
      
      source.connect(processor);
      processor.connect(audioContextRef.current.destination);
      
      continuousRecordingRef.current = true;
      setDialogState(prev => ({ ...prev, isRecording: true }));
      
    } catch (error) {
      console.error('启动连续录制失败:', error);
      toast.error('无法访问麦克风');
    }
  };
  
  // 发送连续音频数据
  const sendContinuousAudioData = async (audioData: number[]) => {
    if (!connection || !sessionId || !continuousRecordingRef.current) {
      return;
    }
    
    try {
      await connection.invoke('SendAudio', sessionId, audioData, false);
    } catch (error) {
      console.error('发送连续音频失败:', error);
    }
  };
  
  // 停止连续录制
  const stopBrowserRecording = () => {
    continuousRecordingRef.current = false;
    setDialogState(prev => ({ ...prev, isRecording: false }));
    
    if (audioStreamRef.current) {
      audioStreamRef.current.getTracks().forEach(track => track.stop());
      audioStreamRef.current = null;
    }
    
    if (audioContextRef.current) {
      audioContextRef.current.close();
      audioContextRef.current = null;
    }
  };
  

  
  // 发送ChatTTSText
  const sendChatTtsText = async () => {
    if (!connection || !sessionId || !chatText.trim()) {
      toast.error('请先创建会话并输入文本');
      return;
    }
    
    try {
      setDialogState(prev => ({ ...prev, isSendingChatTts: true }));
      const success = await connection.invoke('SendChatTtsText', sessionId, chatText.trim());
      if (success) {
        toast.success('文本已发送');
        setChatText('');
      } else {
        toast.error('发送失败');
        setDialogState(prev => ({ ...prev, isSendingChatTts: false }));
      }
    } catch (error) {
      console.error('发送ChatTtsText失败:', error);
      toast.error('发送失败');
      setDialogState(prev => ({ ...prev, isSendingChatTts: false }));
    }
  };
  
  // 通话时长计时器
  useEffect(() => {
    if (dialogState.callState === CallState.CALLING && dialogState.callStartTime) {
      callTimerRef.current = setInterval(() => {
        const elapsed = Math.floor((Date.now() - dialogState.callStartTime!) / 1000);
        setCallDuration(elapsed);
      }, 1000);
    } else {
      if (callTimerRef.current) {
        clearInterval(callTimerRef.current);
        callTimerRef.current = null;
      }
      setCallDuration(0);
    }
    
    return () => {
      if (callTimerRef.current) {
        clearInterval(callTimerRef.current);
      }
    };
  }, [dialogState.callState, dialogState.callStartTime]);
  
  // 清理资源
  useEffect(() => {
    return () => {
      // 组件卸载时清理资源
      clearAudioQueue();
      stopBrowserRecording();
      if (callTimerRef.current) {
        clearInterval(callTimerRef.current);
      }
    };
  }, []);
  
  // 监听dialogState变化，处理音频队列
  useEffect(() => {
    if (dialogState.isUserQuerying) {
      // 用户开始说话时，清空音频队列
      clearAudioQueue();
    }
  }, [dialogState.isUserQuerying]);
  
  // 获取通话状态显示文本
  const getCallStateText = () => {
    switch (dialogState.callState) {
      case CallState.IDLE: return '待机中';
      case CallState.CONNECTING: return '连接中...';
      case CallState.DIALING: return '拨打中...';
      case CallState.CALLING: return '通话中';
      case CallState.ENDING: return '挂断中...';
      case CallState.ERROR: return '连接失败';
      default: return '未知状态';
    }
  };
  
  // 获取通话状态颜色
  const getCallStateColor = () => {
    switch (dialogState.callState) {
      case CallState.IDLE: return '#666';
      case CallState.CONNECTING: return '#1890ff';
      case CallState.DIALING: return '#52c41a';
      case CallState.CALLING: return '#52c41a';
      case CallState.ENDING: return '#ff4d4f';
      case CallState.ERROR: return '#ff4d4f';
      default: return '#666';
    }
  };
  
  // 音频可视化组件
  const AudioVisualization = () => {
    const bars = Array.from({ length: 5 }, (_, i) => {
      const height = dialogState.isRecording || dialogState.isAiSpeaking 
        ? Math.random() * 40 + 10 
        : 5;
      return (
        <div
          key={i}
          className="bg-white rounded-full transition-all duration-200"
          style={{
            width: '4px',
            height: `${height}px`,
            opacity: dialogState.callState === CallState.CALLING ? 1 : 0.3
          }}
        />
      );
    });
    
    return (
      <div className="flex items-center justify-center gap-1 h-12">
        {bars}
      </div>
    );
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-800 via-slate-700 to-slate-900 flex items-center justify-center p-8">
      {/* iPhone 外观容器 */}
      <div className="relative">
        {/* iPhone 边框和阴影 */}
         <div className="relative w-[375px] h-[812px] bg-black rounded-[60px] p-2 shadow-2xl" style={{
           boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.8), 0 0 0 1px rgba(255, 255, 255, 0.1), inset 0 1px 0 rgba(255, 255, 255, 0.1)'
         }}>
           {/* 侧边按钮 */}
           <div className="absolute -left-1 top-20 w-1 h-8 bg-gray-800 rounded-l-sm"></div>
           <div className="absolute -left-1 top-32 w-1 h-12 bg-gray-800 rounded-l-sm"></div>
           <div className="absolute -left-1 top-48 w-1 h-12 bg-gray-800 rounded-l-sm"></div>
           <div className="absolute -right-1 top-28 w-1 h-16 bg-gray-800 rounded-r-sm"></div>
           
           {/* iPhone 屏幕 */}
           <div className="w-full h-full bg-gradient-to-br from-blue-900 via-purple-900 to-indigo-900 rounded-[50px] overflow-hidden relative">
             {/* 刘海 */}
             <div className="absolute top-0 left-1/2 transform -translate-x-1/2 w-36 h-6 bg-black rounded-b-2xl z-10">
               {/* 听筒 */}
               <div className="absolute top-2 left-1/2 transform -translate-x-1/2 w-12 h-1 bg-gray-700 rounded-full"></div>
               {/* 前置摄像头 */}
               <div className="absolute top-1.5 right-4 w-2 h-2 bg-gray-800 rounded-full"></div>
             </div>
             
             {/* Home指示器 */}
             <div className="absolute bottom-2 left-1/2 transform -translate-x-1/2 w-32 h-1 bg-white/30 rounded-full"></div>
            
            {/* 实际的通话界面 */}
            <div className="flex flex-col h-full">
              {/* 顶部状态栏 */}
              <div className="flex justify-between items-center p-4 pt-8 text-white">
                <div className="flex items-center gap-2">
                  <User className="w-4 h-4" />
                  <span className="text-sm font-medium">豆包语音助手</span>
                </div>
                <Button
                  type="text"
                  icon={<Settings className="w-4 h-4" />}
                  onClick={() => setShowSettings(true)}
                  className="text-white hover:bg-white/10 border-none"
                  size="small"
                />
              </div>
              
              {/* 主要通话界面 */}
              <div className="flex-1 flex flex-col items-center justify-center px-4">
                {/* 头像区域 */}
                <div className="relative mb-6">
                  <div className="w-32 h-32 rounded-full bg-gradient-to-br from-blue-400 to-purple-500 flex items-center justify-center shadow-xl">
                    <User className="w-16 h-16 text-white" />
                  </div>
          
          {/* 通话状态指示环 */}
          {dialogState.callState === CallState.CALLING && (
            <div className="absolute inset-0 rounded-full border-4 border-green-400 animate-pulse" />
          )}
          
          {/* 连接状态指示环 */}
          {(dialogState.callState === CallState.CONNECTING || dialogState.callState === CallState.DIALING) && (
            <div className="absolute inset-0 rounded-full border-4 border-blue-400 animate-spin" style={{
              borderTopColor: 'transparent',
              borderRightColor: 'transparent'
            }} />
          )}
        </div>
        
                {/* 通话状态文本 */}
                <div className="text-center mb-4">
                  <h2 className="text-lg font-light text-white mb-2">
                    {getCallStateText()}
                  </h2>
                  
                  {/* 通话时长 */}
                  {dialogState.callState === CallState.CALLING && (
                    <p className="text-sm text-white/80">
                      {formatCallDuration(callDuration)}
                    </p>
                  )}
                </div>
                
                {/* 音频可视化 */}
                {dialogState.callState === CallState.CALLING && (
                  <div className="mb-6">
                    <AudioVisualization />
                  </div>
                )}
                
                {/* 状态指示器 */}
                {dialogState.callState === CallState.CALLING && (
                  <div className="flex flex-col gap-2 mb-6">
                    {dialogState.isUserQuerying && (
                      <div className="flex items-center gap-2 bg-green-500/20 px-2 py-1 rounded-full">
                        <div className="w-1.5 h-1.5 bg-green-400 rounded-full animate-pulse" />
                        <span className="text-xs text-green-300">用户说话中</span>
                      </div>
                    )}
                    
                    {dialogState.isAiSpeaking && (
                      <div className="flex items-center gap-2 bg-blue-500/20 px-2 py-1 rounded-full">
                        <div className="w-1.5 h-1.5 bg-blue-400 rounded-full animate-pulse" />
                        <span className="text-xs text-blue-300">AI回复中</span>
                      </div>
                    )}
                  </div>
                )}
              </div>
              
              {/* 底部控制按钮 */}
              <div className="p-6 pb-8">
                {/* 设置按钮 - 在通话状态为IDLE时显示 */}
                {dialogState.callState === CallState.IDLE && (
                  <div className="flex justify-center mb-4">
                    <Button
                      type="text"
                      size="small"
                      icon={<Settings className="w-4 h-4" />}
                      onClick={() => setShowSettings(true)}
                      className="text-white/60 hover:text-white"
                    >
                      通话设置
                    </Button>
                  </div>
                )}
                
                <div className="flex justify-center items-center gap-6">
                  {/* 静音按钮 */}
                  {dialogState.callState === CallState.CALLING && (
                    <Button
                      type="text"
                      shape="circle"
                      size="large"
                      icon={dialogState.isRecording ? <Mic className="w-5 h-5" /> : <MicOff className="w-5 h-5" />}
                      onClick={dialogState.isRecording ? stopBrowserRecording : startBrowserRecording}
                      className="w-12 h-12 bg-white/10 text-white hover:bg-white/20 border-none"
                    />
                  )}
                  
                  {/* 主要通话按钮 */}
                  <Button
                    type="primary"
                    shape="circle"
                    size="large"
                    icon={
                      dialogState.callState === CallState.IDLE ? 
                        <Phone className="w-6 h-6" /> : 
                        <PhoneOff className="w-6 h-6" />
                    }
                    onClick={() => {
                      console.log('通话按钮被点击');
                      console.log('当前通话状态:', dialogState.callState);
                      if (dialogState.callState === CallState.IDLE) {
                        startCall();
                      } else {
                        endCall();
                      }
                    }}
                    disabled={dialogState.callState === CallState.CONNECTING || dialogState.callState === CallState.ENDING}
                    className={`w-16 h-16 flex items-center justify-center ${
                      dialogState.callState === CallState.IDLE 
                        ? 'bg-green-500 hover:bg-green-600 border-green-500' 
                        : 'bg-red-500 hover:bg-red-600 border-red-500'
                    }`}
                    style={{
                      backgroundColor: dialogState.callState === CallState.IDLE ? '#52c41a' : '#ff4d4f',
                      borderColor: dialogState.callState === CallState.IDLE ? '#52c41a' : '#ff4d4f'
                    }}
                  />
                  
                  {/* 扬声器按钮 */}
                  {dialogState.callState === CallState.CALLING && (
                    <Button
                      type="text"
                      shape="circle"
                      size="large"
                      icon={<Volume2 className="w-5 h-5" />}
                      className="w-12 h-12 bg-white/10 text-white hover:bg-white/20 border-none"
                    />
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      {/* 设置弹窗 */}
      <Modal
        title="通话设置"
        open={showSettings}
        onCancel={() => setShowSettings(false)}
        footer={[
          <Button key="cancel" onClick={() => setShowSettings(false)} size="small">
            取消
          </Button>,
          <Button key="ok" type="primary" onClick={() => setShowSettings(false)} size="small">
            确定
          </Button>
        ]}
        width={320}
        className="settings-modal"
      >
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium mb-1">App ID:</label>
            <Input
              value={sessionConfig.appId}
              onChange={(e) => setSessionConfig(prev => ({ ...prev, appId: e.target.value }))}
              placeholder="请输入App ID"
              size="small"
            />
          </div>
          
          <div>
            <label className="block text-xs font-medium mb-1">Access Key:</label>
            <Input.Password
              value={sessionConfig.accessKey}
              onChange={(e) => setSessionConfig(prev => ({ ...prev, accessKey: e.target.value }))}
              placeholder="请输入Access Key"
              size="small"
            />
          </div>
          
          <div>
             <label className="block text-xs font-medium mb-1">采样率: {sessionConfig.audioConfig.sampleRate}Hz</label>
             <Slider
               min={8000}
               max={48000}
               step={8000}
               value={sessionConfig.audioConfig.sampleRate}
               onChange={(value) => setSessionConfig(prev => ({ ...prev, audioConfig: { ...prev.audioConfig, sampleRate: value } }))}
               marks={{
                 8000: '8K',
                 16000: '16K',
                 24000: '24K',
                 32000: '32K',
                 48000: '48K'
               }}
               size="small"
             />
           </div>
          
          <div>
            <label className="block text-xs font-medium mb-1">声道数:</label>
            <Slider
              min={1}
              max={2}
              value={sessionConfig.audioConfig.channels}
              onChange={(value) => setSessionConfig(prev => ({ ...prev, audioConfig: { ...prev.audioConfig, channels: value } }))}
              marks={{
                1: '单声道',
                2: '立体声'
              }}
              size="small"
            />
          </div>
        </div>
      </Modal>
      
      {/* 调试信息（开发时可见） */}
      {process.env.NODE_ENV === 'development' && (
        <div className="fixed bottom-4 left-4 bg-black/50 text-white p-2 rounded text-xs max-w-xs">
          <div>连接状态: {dialogState.isConnected ? '已连接' : '未连接'}</div>
          <div>会话ID: {sessionId || '无'}</div>
          <div>录音状态: {dialogState.isRecording ? '录音中' : '未录音'}</div>
        </div>
      )}
    </div>
  );
};

export default RealtimeDialog;