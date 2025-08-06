import React, { useState, useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Button, Card, Input, Badge } from 'antd';
import { Divider } from 'antd';
const { TextArea } = Input;
import { Mic, MicOff, Play, Square, Send, Settings } from 'lucide-react';
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

const RealtimeDialog: React.FC = () => {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [sessionId, setSessionId] = useState<string>('');
  const [isRecording, setIsRecording] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [events, setEvents] = useState<DialogEvent[]>([]);
  const [chatText, setChatText] = useState('');
  
  // 配置状态
  const [sessionConfig, setSessionConfig] = useState<SessionConfig>({
    appId: '',
    accessKey: '',
    audioConfig: {
      channels: 1,
      format: 'pcm',
      sampleRate: 16000,
      bitDepth: 16,
      chunkSize: 3200
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
  
  // 初始化SignalR连接
  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl('http://localhost:5094/hubs/realtime-dialog')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();
    
    setConnection(newConnection);
    
    return () => {
      if (newConnection) {
        newConnection.stop();
      }
    };
  }, []);
  
  // 设置SignalR事件监听
  useEffect(() => {
    if (connection) {
      // 连接状态事件
      connection.onreconnecting(() => {
        setIsConnected(false);
        toast.info('正在重新连接...');
      });
      
      connection.onreconnected(() => {
        setIsConnected(true);
        toast.success('重新连接成功');
      });
      
      connection.onclose(() => {
        setIsConnected(false);
        toast.error('连接已断开');
      });
      
      // 对话事件监听
      connection.on('OnAsrInfo', (data) => {
        addEvent('ASR_INFO', data);
        toast.info('检测到语音输入');
      });
      
      connection.on('OnAsrResponse', (data) => {
        addEvent('ASR_RESPONSE', data);
        toast.success(`识别结果: ${data.text || ''}`);
      });
      
      connection.on('OnAsrEnded', (data) => {
        addEvent('ASR_ENDED', data);
        toast.info('语音识别结束');
      });
      
      connection.on('OnTtsResponse', (data) => {
        addEvent('TTS_RESPONSE', data);
        if (data.audioData) {
          playAudioData(data.audioData);
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
  
  // 连接到SignalR Hub
  const connectToHub = async () => {
    if (connection) {
      try {
        await connection.start();
        setIsConnected(true);
        toast.success('连接成功');
      } catch (error) {
        console.error('连接失败:', error);
        toast.error('连接失败');
      }
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
    if (!connection || !isConnected) {
      toast.error('请先连接到服务器');
      return;
    }
    
    if (!sessionConfig.appId || !sessionConfig.accessKey) {
      toast.error('请填写AppId和AccessKey');
      return;
    }
    
    try {
      const newSessionId = await connection.invoke('CreateSession', sessionConfig);
      if (newSessionId) {
        setSessionId(newSessionId);
        toast.success('会话创建成功');
      } else {
        toast.error('会话创建失败');
      }
    } catch (error) {
      console.error('创建会话失败:', error);
      toast.error('创建会话失败');
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
  
  // 浏览器录制
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
      
      audioContextRef.current = new AudioContext({ sampleRate: sessionConfig.audioConfig.sampleRate });
      
      mediaRecorderRef.current = new MediaRecorder(stream, {
        mimeType: 'audio/webm;codecs=opus'
      });
      
      audioChunksRef.current = [];
      
      mediaRecorderRef.current.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
          // 这里可以实时发送音频数据到服务器
          sendAudioChunk(event.data);
        }
      };
      
      mediaRecorderRef.current.start(100); // 每100ms收集一次数据
    } catch (error) {
      console.error('启动浏览器录制失败:', error);
      toast.error('无法访问麦克风');
    }
  };
  
  // 停止浏览器录制
  const stopBrowserRecording = () => {
    if (mediaRecorderRef.current) {
      mediaRecorderRef.current.stop();
      mediaRecorderRef.current.stream.getTracks().forEach(track => track.stop());
    }
    
    if (audioContextRef.current) {
      audioContextRef.current.close();
    }
  };
  
  // 发送音频块
  const sendAudioChunk = async (audioBlob: Blob) => {
    if (!connection || !sessionId) {
      return;
    }
    
    try {
      const arrayBuffer = await audioBlob.arrayBuffer();
      const audioData = new Uint8Array(arrayBuffer);
      
      await connection.invoke('SendAudio', sessionId, audioData, false);
    } catch (error) {
      console.error('发送音频失败:', error);
    }
  };
  
  // 发送ChatTTSText
  const sendChatTtsText = async () => {
    if (!connection || !sessionId || !chatText.trim()) {
      toast.error('请先创建会话并输入文本');
      return;
    }
    
    try {
      const success = await connection.invoke('SendChatTtsText', sessionId, chatText.trim());
      if (success) {
        toast.success('文本已发送');
        setChatText('');
      } else {
        toast.error('发送失败');
      }
    } catch (error) {
      console.error('发送ChatTtsText失败:', error);
      toast.error('发送失败');
    }
  };
  
  // 播放音频数据
  const playAudioData = async (audioData: number[]) => {
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
      source.start();
      
      toast.success('播放音频');
    } catch (error) {
      console.error('播放音频失败:', error);
      toast.error('播放音频失败');
    }
  };
  
  return (
    <div className="container mx-auto p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">豆包实时语音对话</h1>
        <div className="flex items-center space-x-2">
          <Badge color={isConnected ? 'green' : 'default'}>
            {isConnected ? '已连接' : '未连接'}
          </Badge>
          {sessionId && (
            <Badge color="blue">
              会话: {sessionId.slice(0, 8)}...
            </Badge>
          )}
        </div>
      </div>
      
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* 连接和会话控制 */}
        <Card 
          title={
            <div className="flex items-center space-x-2">
              <Settings className="w-5 h-5" />
              <span>连接控制</span>
            </div>
          }
          className="mb-6"
        >
          <div className="space-y-4">
            <div className="flex space-x-2">
              <Button 
                onClick={connectToHub} 
                disabled={isConnected}
                className="flex-1"
              >
                连接服务器
              </Button>
              <Button 
                onClick={disconnectFromHub} 
                disabled={!isConnected}
                variant="outlined"
                className="flex-1"
              >
                断开连接
              </Button>
            </div>
            
            <Divider />
            
            <div className="space-y-2">
              <label htmlFor="appId">App ID</label>
              <Input
                id="appId"
                value={sessionConfig.appId}
                onChange={(e) => setSessionConfig(prev => ({ ...prev, appId: e.target.value }))}
                placeholder="请输入豆包App ID"
              />
            </div>
            
            <div className="space-y-2">
              <label htmlFor="accessKey">Access Key</label>
              <Input
                id="accessKey"
                type="password"
                value={sessionConfig.accessKey}
                onChange={(e) => setSessionConfig(prev => ({ ...prev, accessKey: e.target.value }))}
                placeholder="请输入Access Key"
              />
            </div>
            
            <div className="flex space-x-2">
              <Button 
                onClick={createSession} 
                disabled={!isConnected || !!sessionId}
                className="flex-1"
              >
                创建会话
              </Button>
              <Button 
                onClick={endSession} 
                disabled={!sessionId}
                danger
                className="flex-1"
              >
                结束会话
              </Button>
            </div>
          </div>
        </Card>
        
        {/* 音频控制 */}
        <Card 
          title={
            <div className="flex items-center space-x-2">
              <Mic className="w-5 h-5" />
              <span>音频控制</span>
            </div>
          }
          className="mb-6"
        >
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label>采样率</label>
                  <Input
                    type="number"
                    value={sessionConfig.audioConfig.sampleRate}
                    onChange={(e) => setSessionConfig(prev => ({ 
                      ...prev, 
                      audioConfig: { ...prev.audioConfig, sampleRate: parseInt(e.target.value) }
                    }))}
                  />
                </div>
                <div className="space-y-2">
                  <label>声道数</label>
                  <Input
                    type="number"
                    value={sessionConfig.audioConfig.channels}
                    onChange={(e) => setSessionConfig(prev => ({ 
                      ...prev, 
                      audioConfig: { ...prev.audioConfig, channels: parseInt(e.target.value) }
                    }))}
                  />
                </div>
              </div>
            
            <div className="flex space-x-2">
              <Button 
                onClick={startRecording} 
                disabled={!sessionId || isRecording}
                className="flex-1"
                danger={isRecording}
              >
                {isRecording ? (
                  <>
                    <Square className="w-4 h-4 mr-2" />
                    录制中...
                  </>
                ) : (
                  <>
                    <Mic className="w-4 h-4 mr-2" />
                    开始录制
                  </>
                )}
              </Button>
              <Button 
                onClick={stopRecording} 
                disabled={!isRecording}
                variant="outlined"
                className="flex-1"
              >
                <MicOff className="w-4 h-4 mr-2" />
                停止录制
              </Button>
            </div>
            
            <Divider />
            
            <div className="space-y-2">
              <label htmlFor="chatText">ChatTTS文本</label>
              <div className="flex space-x-2">
                <TextArea
                  id="chatText"
                  value={chatText}
                  onChange={(e) => setChatText(e.target.value)}
                  placeholder="输入要合成的文本..."
                  className="flex-1"
                  rows={3}
                />
                <Button 
                  onClick={sendChatTtsText}
                  disabled={!sessionId || !chatText.trim()}
                  size="small"
                  icon={<Send className="w-4 h-4" />}
                />
              </div>
            </div>
          </div>
        </Card>
      </div>
      
      {/* 事件日志 */}
      <Card 
        title="事件日志"
        className="mb-6"
      >
        <div>
          <div className="max-h-96 overflow-y-auto space-y-2">
            {events.length === 0 ? (
              <p className="text-muted-foreground text-center py-8">
                暂无事件
              </p>
            ) : (
              events.map((event) => (
                <div key={event.id} className="p-3 border rounded-lg">
                  <div className="flex items-center justify-between mb-2">
                    <Badge color="default">{event.type}</Badge>
                    <span className="text-sm text-muted-foreground">{event.timestamp}</span>
                  </div>
                  <pre className="text-sm bg-muted p-2 rounded overflow-x-auto">
                    {JSON.stringify(event.data, null, 2)}
                  </pre>
                </div>
              ))
            )}
          </div>
        </div>
      </Card>
    </div>
  );
};

export default RealtimeDialog;