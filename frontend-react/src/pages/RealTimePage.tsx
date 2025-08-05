import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Button, Card, Space, Typography, Alert, Spin, Progress, Select, Slider, notification } from 'antd';
import { 
  PhoneOutlined, 
  PhoneFilled, 
  AudioOutlined, 
  AudioMutedOutlined,
  SettingOutlined,
  SoundOutlined
} from '@ant-design/icons';
import { RealTimeApiService } from '../services/realTimeApiService';
import { audioService, AudioDeviceInfo, AudioPermissions } from '../services/audioService';
import { configService } from '../services/configService';
import { 
  RealTimeConnectionState, 
  CreateSessionRequest,
  StartSessionRequest,
  AudioVisualizationData,
  VoiceMode 
} from '../types';
import { 
  REALTIME_CONFIG, 
  SUCCESS_MESSAGES, 
  REALTIME_ERROR_MESSAGES 
} from '../constants';
import AudioVisualization from '../components/AudioVisualization';
import { handleError, showSuccess, showInfo } from '../utils/errorHandler';
import { uxManager, performOperation, setLoading, isLoading, withRetry } from '../utils/userExperience';
import './RealTimePage.css';

const { Title, Text } = Typography;
const { Option } = Select;

interface CallState {
  isConnecting: boolean;
  isConnected: boolean;
  isInCall: boolean;
  isMuted: boolean;
  volume: number;
  duration: number;
}

const RealTimePage: React.FC = () => {
  // 状态管理
  const [callState, setCallState] = useState<CallState>({
    isConnecting: false,
    isConnected: false,
    isInCall: false,
    isMuted: false,
    volume: 0.8,
    duration: 0
  });
  
  const [connectionState, setConnectionState] = useState<RealTimeConnectionState>(
    RealTimeConnectionState.Disconnected
  );
  
  const [audioPermissions, setAudioPermissions] = useState<AudioPermissions>({
    microphone: false,
    speaker: false
  });
  
  const [audioDevices, setAudioDevices] = useState<AudioDeviceInfo[]>([]);
  const [selectedMicrophone, setSelectedMicrophone] = useState<string>('');
  const [selectedSpeaker, setSelectedSpeaker] = useState<string>('');
  
  const [visualizationData, setVisualizationData] = useState<AudioVisualizationData>({
    frequencies: [],
    volume: 0,
    isRecording: false,
    isPlaying: false
  });
  
  const [error, setError] = useState<string>('');
  const [isLoading, setIsLoading] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  const [retryCount, setRetryCount] = useState(0);
  
  // Refs
  const realTimeApiServiceRef = useRef<RealTimeApiService | null>(null);
  const callTimerRef = useRef<NodeJS.Timeout | null>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  
  // 初始化
  useEffect(() => {
    initializeServices();
    return () => {
      cleanup();
    };
  }, []);
  
  // 通话计时器
  useEffect(() => {
    if (callState.isInCall) {
      callTimerRef.current = setInterval(() => {
        setCallState(prev => ({ ...prev, duration: prev.duration + 1 }));
      }, 1000);
    } else {
      if (callTimerRef.current) {
        clearInterval(callTimerRef.current);
        callTimerRef.current = null;
      }
      setCallState(prev => ({ ...prev, duration: 0 }));
    }
    
    return () => {
      if (callTimerRef.current) {
        clearInterval(callTimerRef.current);
      }
    };
  }, [callState.isInCall]);
  
  // 音频可视化
  useEffect(() => {
    if ((visualizationData.isRecording || visualizationData.isPlaying) && canvasRef.current) {
      drawVisualization();
    }
  }, [visualizationData]);
  
  const initializeServices = async () => {
    await performOperation(
      async () => {
        // 初始化配置服务
        await configService.initialize();
        
        // 请求音频权限
        const permissions = await audioService.requestPermissions();
        setAudioPermissions(permissions);
        
        if (!permissions.microphone) {
          throw new Error('用户拒绝了麦克风权限');
        }
        
        // 获取音频设备
        const devices = await audioService.getAudioDevices();
        setAudioDevices(devices);
        
        // 设置默认设备
        const microphones = devices.filter(d => d.kind === 'audioinput');
        const speakers = devices.filter(d => d.kind === 'audiooutput');
        
        if (microphones.length > 0) {
          setSelectedMicrophone(microphones[0].deviceId);
        }
        if (speakers.length > 0) {
          setSelectedSpeaker(speakers[0].deviceId);
        }
        
        // 初始化音频上下文
        await audioService.initializeAudioContext(microphones[0]?.deviceId);
        
        // 设置音频事件监听器
        setupAudioEventListeners();
        
        // 创建实时语音API服务
        const config = configService.getAppConfig();
        realTimeApiServiceRef.current = new RealTimeApiService(config.apiBaseUrl);
        setupRealTimeEventListeners();
        
        showSuccess('音频服务初始化成功');
      },
      {
        loadingKey: 'service-init',
        loadingMessage: '正在初始化音频服务...',
        errorContext: 'service_initialization',
        retry: {
          maxAttempts: 3,
          delay: 1000,
          onRetry: (attempt) => {
            setRetryCount(attempt);
            showInfo(`正在重试初始化服务 (${attempt}/3)`);
          }
        }
      }
    ).catch((error) => {
      setError(error instanceof Error ? error.message : '初始化失败');
    });
  };
  
  const setupAudioEventListeners = () => {
    audioService.addEventListener('visualizationUpdate', (data: AudioVisualizationData) => {
      setVisualizationData(data);
    });
    
    audioService.addEventListener('recordingStarted', () => {
      console.log('录音已开始');
    });
    
    audioService.addEventListener('recordingStopped', () => {
      console.log('录音已停止');
    });
    
    audioService.addEventListener('audioData', async (blob: Blob) => {
      if (realTimeApiServiceRef.current && realTimeApiServiceRef.current.isWebSocketConnected()) {
        try {
          const arrayBuffer = await audioService.convertBlobToArrayBuffer(blob);
          const pcmData = await audioService.convertAudioToPCM(arrayBuffer);
          realTimeApiServiceRef.current.sendAudioData(pcmData);
        } catch (error) {
          console.error('发送音频数据失败:', error);
        }
      }
    });
    
    audioService.addEventListener('error', (error: Error) => {
      setError(error.message);
    });
  };
  
  const setupRealTimeEventListeners = () => {
    if (!realTimeApiServiceRef.current) return;
    
    realTimeApiServiceRef.current.addEventListener('connectionStateChanged', (event: any) => {
      const state = event.newState;
      setConnectionState(state);
      setCallState(prev => ({
        ...prev,
        isConnecting: state === RealTimeConnectionState.Connecting,
        isConnected: state === RealTimeConnectionState.Connected
      }));
    });
    
    realTimeApiServiceRef.current.addEventListener('sessionStarted', () => {
      setCallState(prev => ({ ...prev, isInCall: true }));
    });
    
    realTimeApiServiceRef.current.addEventListener('sessionEnded', () => {
      setCallState(prev => ({ ...prev, isInCall: false }));
    });
    
    realTimeApiServiceRef.current.addEventListener('audioData', async (audioEvent: any) => {
      try {
        await audioService.playAudioData(audioEvent.data);
      } catch (error) {
        console.error('播放音频失败:', error);
      }
    });
    
    realTimeApiServiceRef.current.addEventListener('error', (error: any) => {
      setError(error.message || '发生未知错误');
    });
  };
  
  const handleStartCall = async () => {
    if (!realTimeApiServiceRef.current || !audioPermissions.microphone) {
      setError(REALTIME_ERROR_MESSAGES.AUDIO_PERMISSION_DENIED);
      return;
    }

    await performOperation(
      async () => {
        // 1. 创建会话
        const config = configService.getConfig();
        const createSessionRequest: CreateSessionRequest = {
          appId: config.app.appId,
          accessToken: config.app.accessToken,
          webSocketUrl: config.app.webSocketUrl,
          connectionTimeoutMs: config.app.connectionTimeoutMs,
          audioBufferSeconds: config.app.audioBufferSeconds
        };
        
        const sessionResponse = await realTimeApiServiceRef.current!.createSession(createSessionRequest);
        
        // 2. 启动会话
        const startSessionRequest: StartSessionRequest = {
          botName: config.app.botName,
          systemRole: config.app.systemRole,
          speakingStyle: config.app.speakingStyle,
          audioConfig: {
            channel: config.audio.defaultChannels,
            format: config.audio.defaultFormat,
            sampleRate: config.audio.defaultSampleRate
          }
        };
        
        await realTimeApiServiceRef.current!.startSession(sessionResponse.sessionId, startSessionRequest);
        
        // 3. 连接WebSocket
        await withRetry(
          () => realTimeApiServiceRef.current!.connectWebSocket(sessionResponse.sessionId),
          {
            maxAttempts: 3,
            delay: 2000,
            onRetry: (attempt) => {
              setRetryCount(attempt);
              showInfo(`正在重试连接 (${attempt}/3)`);
            }
          },
          'websocket-connect'
        );
        
        // 5. 发送问候
        await realTimeApiServiceRef.current!.sayHello(sessionResponse.sessionId, {
          content: config.app.greetingMessage
        });
        
        // 5. 开始录音
        await audioService.startRecording();
      },
      {
        loadingKey: 'call-start',
        loadingMessage: '正在建立连接...',
        successMessage: '通话已开始',
        errorContext: 'call_start',
        showProgress: true
      }
    ).catch((error) => {
      setError(error instanceof Error ? error.message : '开始通话失败');
    });
  };
  
  const handleEndCall = async () => {
    await performOperation(
      async () => {
        // 停止录音
        audioService.stopRecording();
        
        // 结束会话
        if (realTimeApiServiceRef.current && realTimeApiServiceRef.current.getSessionId()) {
          await realTimeApiServiceRef.current.finishSession(realTimeApiServiceRef.current.getSessionId()!);
          realTimeApiServiceRef.current.disconnectWebSocket();
        }
        
        // 重置重试计数
        setRetryCount(0);
      },
      {
        loadingKey: 'call-end',
        loadingMessage: '正在结束通话...',
        successMessage: '通话已结束',
        errorContext: 'call_end'
      }
    ).catch((error) => {
      setError(error instanceof Error ? error.message : '结束通话失败');
    });
  };
  
  const handleToggleMute = () => {
    setCallState(prev => ({ ...prev, isMuted: !prev.isMuted }));
    // TODO: 实现静音逻辑
  };
  
  const handleVolumeChange = (value: number) => {
    setCallState(prev => ({ ...prev, volume: value / 100 }));
    audioService.setVolume(value / 100);
  };
  
  const handleMicrophoneChange = async (deviceId: string) => {
    await performOperation(
      async () => {
        setSelectedMicrophone(deviceId);
        await audioService.changeAudioDevice(deviceId);
      },
      {
        loadingKey: 'device-change',
        loadingMessage: '正在切换麦克风...',
        successMessage: '麦克风切换成功',
        errorContext: 'device_change'
      }
    ).catch((error) => {
      setError('切换麦克风失败');
    });
  };
  
  const drawVisualization = () => {
    const canvas = canvasRef.current;
    if (!canvas || !visualizationData.frequencies || !visualizationData.frequencies.length) return;
    
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    const width = canvas.width;
    const height = canvas.height;
    const barCount = visualizationData.frequencies.length;
    const barWidth = width / barCount;
    
    ctx.clearRect(0, 0, width, height);
    
    // 绘制频谱
    for (let i = 0; i < barCount; i++) {
      const barHeight = (visualizationData.frequencies[i] / 255) * height;
      const hue = (i / barCount) * 360;
      
      ctx.fillStyle = `hsl(${hue}, 70%, 60%)`;
      ctx.fillRect(i * barWidth, height - barHeight, barWidth - 1, barHeight);
    }
  };
  
  const formatDuration = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };
  
  const cleanup = () => {
    if (callTimerRef.current) {
      clearInterval(callTimerRef.current);
    }
    
    audioService.cleanup();
    
    if (realTimeApiServiceRef.current) {
      realTimeApiServiceRef.current.dispose();
    }
  };
  
  const getConnectionStatusText = () => {
    switch (connectionState) {
      case RealTimeConnectionState.Connecting:
        return '连接中...';
      case RealTimeConnectionState.Connected:
        return '已连接';
      case RealTimeConnectionState.Disconnecting:
        return '断开连接中...';
      default:
        return '未连接';
    }
  };
  
  const getConnectionStatusColor = () => {
    switch (connectionState) {
      case RealTimeConnectionState.Connected:
        return '#52c41a';
      case RealTimeConnectionState.Connecting:
      case RealTimeConnectionState.Disconnecting:
        return '#faad14';
      default:
        return '#ff4d4f';
    }
  };
  
  if (isLoading) {
    return (
      <div className="realtime-page loading">
        <Spin size="large" />
        <Text>正在初始化音频服务...</Text>
      </div>
    );
  }
  
  return (
    <div className="realtime-page">
      <div className="realtime-container">
        <Card className="call-interface">
          {/* 标题和状态 */}
          <div className="call-header">
            <Title level={2}>实时语音对话</Title>
            <div className="connection-status">
              <div 
                className="status-indicator"
                style={{ backgroundColor: getConnectionStatusColor() }}
              />
              <Text>{getConnectionStatusText()}</Text>
              {callState.isInCall && callState.duration > 0 && (
                <div className="call-duration-header">
                  <Text>通话时长: {formatDuration(callState.duration)}</Text>
                </div>
              )}
              {retryCount > 0 && (
                <div className="retry-info">
                  <Text>重试次数: {retryCount}</Text>
                </div>
              )}
            </div>
          </div>
          
          {/* 错误提示 */}
          {error && (
            <Alert
              message={error}
              type="error"
              closable
              onClose={() => setError('')}
              style={{ marginBottom: 16 }}
              action={
                <Button size="small" onClick={() => {
                  setError('');
                  if (connectionState === RealTimeConnectionState.Disconnected) {
                    handleStartCall();
                  }
                }}>
                  重试
                </Button>
              }
            />
          )}
          
          {isLoading && (
            <Alert
              message="正在处理"
              description="正在执行操作，请稍候..."
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
            />
          )}
          
          {/* 权限检查 */}
          {!audioPermissions.microphone && (
            <Alert
              message="需要麦克风权限才能进行语音对话"
              type="warning"
              action={
                <Button size="small" onClick={initializeServices}>
                  重新授权
                </Button>
              }
              style={{ marginBottom: 16 }}
            />
          )}
          
          {/* 通话界面 */}
          <div className="call-main">
            {/* 音频可视化 */}
            <AudioVisualization
              visualizationData={visualizationData}
              isRecording={audioService.isCurrentlyRecording()}
              isPlaying={audioService.isCurrentlyPlaying()}
              width={300}
              height={100}
              showVolumeBar={true}
              showFrequencyBars={true}
              theme="light"
              className={`main-visualization ${
                callState.isInCall ? 'active' : ''
              } ${
                error ? 'error' : ''
              }`}
            />
            
            {/* 通话时长 */}
            {callState.isInCall && (
              <div className="call-duration">
                <Text className="duration-text">
                  {formatDuration(callState.duration)}
                </Text>
              </div>
            )}
            
            {/* 控制按钮 */}
            <div className="call-controls">
              <Space size="large">
                {/* 静音按钮 */}
                <Button
                  type={callState.isMuted ? "primary" : "default"}
                  shape="circle"
                  size="large"
                  icon={callState.isMuted ? <AudioMutedOutlined /> : <AudioOutlined />}
                  onClick={handleToggleMute}
                  disabled={!callState.isInCall}
                  className="control-button mute-button"
                />
                
                {/* 主通话按钮 */}
                <Button
                  type={callState.isInCall ? "primary" : "default"}
                  shape="circle"
                  size="large"
                  icon={callState.isInCall ? <PhoneFilled /> : <PhoneOutlined />}
                  onClick={callState.isInCall ? handleEndCall : handleStartCall}
                  loading={callState.isConnecting}
                  className={`control-button main-call-button ${
                    callState.isInCall ? 'end-call' : 'start-call'
                  }`}
                  disabled={!audioPermissions.microphone}
                />
                
                {/* 设置按钮 */}
                <Button
                  type="default"
                  shape="circle"
                  size="large"
                  icon={<SettingOutlined />}
                  onClick={() => setShowSettings(!showSettings)}
                  className="control-button settings-button"
                />
              </Space>
            </div>
          </div>
          
          {/* 设置面板 */}
          {showSettings && (
            <Card className="settings-panel" size="small">
              <Title level={4}>音频设置</Title>
              
              <div className="setting-item">
                <Text>麦克风:</Text>
                <Select
                  value={selectedMicrophone}
                  onChange={handleMicrophoneChange}
                  style={{ width: '100%', marginTop: 8 }}
                  placeholder="选择麦克风"
                >
                  {audioDevices
                    .filter(device => device.kind === 'audioinput')
                    .map(device => (
                      <Option key={device.deviceId} value={device.deviceId}>
                        {device.label}
                      </Option>
                    ))
                  }
                </Select>
              </div>
              
              <div className="setting-item">
                <Text>扬声器:</Text>
                <Select
                  value={selectedSpeaker}
                  onChange={setSelectedSpeaker}
                  style={{ width: '100%', marginTop: 8 }}
                  placeholder="选择扬声器"
                >
                  {audioDevices
                    .filter(device => device.kind === 'audiooutput')
                    .map(device => (
                      <Option key={device.deviceId} value={device.deviceId}>
                        {device.label}
                      </Option>
                    ))
                  }
                </Select>
              </div>
              
              <div className="setting-item">
                <Text>音量:</Text>
                <div style={{ display: 'flex', alignItems: 'center', marginTop: 8 }}>
                  <SoundOutlined style={{ marginRight: 8 }} />
                  <Slider
                    min={0}
                    max={100}
                    value={Math.round(callState.volume * 100)}
                    onChange={handleVolumeChange}
                    style={{ flex: 1 }}
                  />
                  <Text style={{ marginLeft: 8, minWidth: 35 }}>
                    {Math.round(callState.volume * 100)}%
                  </Text>
                </div>
              </div>
            </Card>
          )}
        </Card>
      </div>
    </div>
  );
};

export default RealTimePage;