import { API_ENDPOINTS } from '../constants';
import { 
  CreateSessionRequest, 
  CreateSessionResponse,
  StartSessionRequest,
  SessionStatusResponse,
  SayHelloRequest,
  RealTimeConnectionState,
  AudioDataEvent,
  DialogEvent,
  ConnectionStateEvent
} from '../types';

/**
 * 实时语音API服务类
 * 负责HTTP API调用和WebSocket音频流管理
 */
export class RealTimeApiService {
  private baseUrl: string;
  private ws: WebSocket | null = null;
  private sessionId: string | null = null;
  private connectionState: RealTimeConnectionState = RealTimeConnectionState.Disconnected;
  private eventListeners: Map<string, Function[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 1000;

  constructor(baseUrl: string = 'http://localhost:5094') {
    this.baseUrl = baseUrl || window.location.origin;
  }

  // #region Event Management

  /**
   * 添加事件监听器
   */
  public addEventListener(event: string, callback: Function): void {
    if (!this.eventListeners.has(event)) {
      this.eventListeners.set(event, []);
    }
    this.eventListeners.get(event)!.push(callback);
  }

  /**
   * 移除事件监听器
   */
  public removeEventListener(event: string, callback: Function): void {
    const listeners = this.eventListeners.get(event);
    if (listeners) {
      const index = listeners.indexOf(callback);
      if (index > -1) {
        listeners.splice(index, 1);
      }
    }
  }

  /**
   * 触发事件
   */
  private emit(event: string, data?: any): void {
    const listeners = this.eventListeners.get(event) || [];
    listeners.forEach(callback => {
      try {
        callback(data);
      } catch (error) {
        console.error(`Error in event listener for ${event}:`, error);
      }
    });
  }

  /**
   * 设置连接状态
   */
  private setConnectionState(state: RealTimeConnectionState): void {
    if (this.connectionState !== state) {
      const oldState = this.connectionState;
      this.connectionState = state;
      this.emit('connectionStateChanged', {
        oldState,
        newState: state
      } as ConnectionStateEvent);
    }
  }

  // #endregion

  // #region HTTP API Methods

  /**
   * 创建会话
   */
  public async createSession(request: CreateSessionRequest): Promise<CreateSessionResponse> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.CREATE_SESSION}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      this.sessionId = data.sessionId;
      return data;
    } catch (error) {
      console.error('创建会话失败:', error);
      throw error;
    }
  }

  /**
   * 启动会话
   */
  public async startSession(sessionId: string, request: StartSessionRequest): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.START_SESSION.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }

      this.sessionId = sessionId;
    } catch (error) {
      console.error('启动会话失败:', error);
      throw error;
    }
  }

  /**
   * 获取会话状态
   */
  public async getSessionStatus(sessionId: string): Promise<SessionStatusResponse> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.GET_STATUS.replace('{sessionId}', sessionId)}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }

      return await response.json();
    } catch (error) {
      console.error('获取会话状态失败:', error);
      throw error;
    }
  }

  /**
   * 发送问候语
   */
  public async sayHello(sessionId: string, request: SayHelloRequest): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.SAY_HELLO.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('发送问候语失败:', error);
      throw error;
    }
  }

  /**
   * 开始录音
   */
  public async startRecording(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.START_RECORDING.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('开始录音失败:', error);
      throw error;
    }
  }

  /**
   * 停止录音
   */
  public async stopRecording(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.STOP_RECORDING.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('停止录音失败:', error);
      throw error;
    }
  }

  /**
   * 开始播放
   */
  public async startPlayback(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.START_PLAYBACK.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('开始播放失败:', error);
      throw error;
    }
  }

  /**
   * 停止播放
   */
  public async stopPlayback(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.STOP_PLAYBACK.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('停止播放失败:', error);
      throw error;
    }
  }

  /**
   * 结束会话
   */
  public async finishSession(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.FINISH_SESSION.replace('{sessionId}', sessionId)}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
      }

      this.sessionId = null;
    } catch (error) {
      console.error('结束会话失败:', error);
      throw error;
    }
  }

  // #endregion

  // #region WebSocket Management

  /**
   * 连接WebSocket
   */
  public async connectWebSocket(sessionId: string): Promise<void> {
    try {
      if (this.ws && this.ws.readyState === WebSocket.OPEN) {
        console.warn('WebSocket已连接');
        return;
      }

      this.setConnectionState(RealTimeConnectionState.Connecting);

      const wsUrl = `${this.baseUrl.replace('http', 'ws')}${API_ENDPOINTS.REALTIME.WEBSOCKET.replace('{sessionId}', sessionId)}`;
      this.ws = new WebSocket(wsUrl);

      this.ws.onopen = () => {
        console.log('WebSocket连接已建立');
        this.setConnectionState(RealTimeConnectionState.Connected);
        this.reconnectAttempts = 0;
      };

      this.ws.onmessage = (event) => {
        this.handleWebSocketMessage(event);
      };

      this.ws.onclose = (event) => {
        console.log('WebSocket连接已关闭:', event.code, event.reason);
        this.setConnectionState(RealTimeConnectionState.Disconnected);
        
        // 如果不是正常关闭，尝试重连
        if (event.code !== 1000 && this.reconnectAttempts < this.maxReconnectAttempts) {
          this.attemptReconnect(sessionId);
        }
      };

      this.ws.onerror = (error) => {
        console.error('WebSocket错误:', error);
        this.emit('error', { message: 'WebSocket连接错误', error });
      };

    } catch (error) {
      console.error('连接WebSocket失败:', error);
      this.setConnectionState(RealTimeConnectionState.Disconnected);
      throw error;
    }
  }

  /**
   * 断开WebSocket连接
   */
  public disconnectWebSocket(): void {
    if (this.ws) {
      this.setConnectionState(RealTimeConnectionState.Disconnecting);
      this.ws.close(1000, '正常关闭');
      this.ws = null;
    }
  }

  /**
   * 尝试重连
   */
  private attemptReconnect(sessionId: string): void {
    this.reconnectAttempts++;
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
    
    console.log(`尝试重连 (${this.reconnectAttempts}/${this.maxReconnectAttempts})，${delay}ms后重试`);
    
    setTimeout(() => {
      this.connectWebSocket(sessionId).catch(error => {
        console.error('重连失败:', error);
      });
    }, delay);
  }

  /**
   * 处理WebSocket消息
   */
  private handleWebSocketMessage(event: MessageEvent): void {
    try {
      const data = JSON.parse(event.data);
      
      switch (data.type) {
        case 'audio_data':
          this.emit('audioData', {
            data: data.audioData,
            format: 'pcm',
            sampleRate: 24000,
            channels: 1
          } as AudioDataEvent);
          break;
          
        case 'dialog_event':
          this.emit('dialogEvent', {
            type: data.eventType || 'bot_response',
            content: data.content,
            timestamp: new Date()
          } as DialogEvent);
          break;
          
        case 'connection_state':
          this.setConnectionState(data.state);
          break;
          
        case 'error':
          this.emit('error', {
            message: data.message,
            code: data.code
          });
          break;
          
        default:
          console.warn('未知的WebSocket消息类型:', data.type);
      }
    } catch (error) {
      console.error('解析WebSocket消息失败:', error);
    }
  }

  /**
   * 发送音频数据
   */
  public sendAudioData(audioData: ArrayBuffer, sequence?: number): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      const message = {
        type: 'audio_data',
        audioData: Array.from(new Uint8Array(audioData)),
        sequence: sequence || Date.now(),
        timestamp: Date.now()
      };
      
      this.ws.send(JSON.stringify(message));
    } else {
      console.warn('WebSocket未连接，无法发送音频数据');
    }
  }

  // #endregion

  // #region Getters

  /**
   * 获取当前会话ID
   */
  public getSessionId(): string | null {
    return this.sessionId;
  }

  /**
   * 获取连接状态
   */
  public getConnectionState(): RealTimeConnectionState {
    return this.connectionState;
  }

  /**
   * 检查是否已连接
   */
  public isConnected(): boolean {
    return this.connectionState === RealTimeConnectionState.Connected;
  }

  /**
   * 检查WebSocket是否已连接
   */
  public isWebSocketConnected(): boolean {
    return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
  }

  // #endregion

  // #region Cleanup

  /**
   * 清理资源
   */
  public dispose(): void {
    this.disconnectWebSocket();
    this.eventListeners.clear();
    this.sessionId = null;
    this.reconnectAttempts = 0;
  }

  // #endregion
}

// 创建默认实例
export const realTimeApiService = new RealTimeApiService();