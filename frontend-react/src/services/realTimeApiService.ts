import { API_ENDPOINTS } from '../constants';
import { 
  RealTimeConnectionState,
  AudioDataEvent,
  DialogEvent,
  ConnectionStateEvent
} from '../types';

// 新的API请求类型
interface RealTimeConnectRequest {
  appId: string;
  accessToken: string;
  botName?: string;
  systemRole?: string;
  greetingMessage?: string;
  voiceType?: string;
  speed?: number;
  volume?: number;
  pitch?: number;
  audioEncoding?: string;
  sampleRate?: number;
  enableTimestamp?: boolean;
  inputSampleRate?: number;
  outputSampleRate?: number;
  inputChannels?: number;
  outputChannels?: number;
  bufferSize?: number;
  frameSize?: number;
}

interface SendTextRequest {
  text: string;
}

/**
 * 实时语音API服务类
 * 负责HTTP API调用和WebSocket音频流管理
 */
export class RealTimeApiService {
  private baseUrl: string;
  private ws: WebSocket | null = null;
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
   * 连接到实时语音服务
   */
  public async connect(request: RealTimeConnectRequest): Promise<void> {
    try {
      this.setConnectionState(RealTimeConnectionState.Connecting);
      
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.CONNECT}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }

      this.setConnectionState(RealTimeConnectionState.Connected);
    } catch (error) {
      console.error('连接实时语音服务失败:', error);
      this.setConnectionState(RealTimeConnectionState.Disconnected);
      throw error;
    }
  }

  /**
   * 断开实时语音服务连接
   */
  public async disconnect(): Promise<void> {
    try {
      // 先断开WebSocket
      this.disconnectWebSocket();
      
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.DISCONNECT}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }

      this.setConnectionState(RealTimeConnectionState.Disconnected);
    } catch (error) {
      console.error('断开连接失败:', error);
      throw error;
    }
  }

  /**
   * 启用音频功能
   */
  public async enableAudio(): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.ENABLE_AUDIO}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('启用音频失败:', error);
      throw error;
    }
  }

  /**
   * 禁用音频功能
   */
  public async disableAudio(): Promise<void> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.DISABLE_AUDIO}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('禁用音频失败:', error);
      throw error;
    }
  }

  /**
   * 发送文本消息
   */
  public async sendText(text: string): Promise<void> {
    try {
      const request: SendTextRequest = { text };
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.SEND_TEXT}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error('发送文本失败:', error);
      throw error;
    }
  }

  /**
   * 获取连接状态
   */
  public async getStatus(): Promise<{ state: string; timestamp: number }> {
    try {
      const response = await fetch(`${this.baseUrl}${API_ENDPOINTS.REALTIME.STATUS}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
      }

      return await response.json();
    } catch (error) {
      console.error('获取状态失败:', error);
      throw error;
    }
  }

  // #endregion

  // #region WebSocket Management

  /**
   * 连接WebSocket
   */
  public async connectWebSocket(): Promise<void> {
    try {
      if (this.ws && this.ws.readyState === WebSocket.OPEN) {
        console.warn('WebSocket已连接');
        return;
      }

      const wsUrl = `${this.baseUrl.replace('http', 'ws')}${API_ENDPOINTS.REALTIME.WEBSOCKET}`;
      this.ws = new WebSocket(wsUrl);

      this.ws.onopen = () => {
        console.log('WebSocket连接已建立');
        this.reconnectAttempts = 0;
      };

      this.ws.onmessage = (event) => {
        this.handleWebSocketMessage(event);
      };

      this.ws.onclose = (event) => {
        console.log('WebSocket连接已关闭:', event.code, event.reason);
        
        // 如果不是正常关闭，尝试重连
        if (event.code !== 1000 && this.reconnectAttempts < this.maxReconnectAttempts) {
          this.attemptReconnect();
        }
      };

      this.ws.onerror = (error) => {
        console.error('WebSocket错误:', error);
        this.emit('error', { message: 'WebSocket连接错误', error });
      };

    } catch (error) {
      console.error('连接WebSocket失败:', error);
      throw error;
    }
  }

  /**
   * 断开WebSocket连接
   */
  public disconnectWebSocket(): void {
    if (this.ws) {
      this.ws.close(1000, '正常关闭');
      this.ws = null;
    }
  }

  /**
   * 尝试重连
   */
  private attemptReconnect(): void {
    this.reconnectAttempts++;
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
    
    console.log(`尝试重连 (${this.reconnectAttempts}/${this.maxReconnectAttempts})，${delay}ms后重试`);
    
    setTimeout(() => {
      this.connectWebSocket().catch(error => {
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
            data: data.data.data,
            timestamp: data.data.timestamp,
            format: 'pcm',
            sampleRate: 16000,
            channels: 1
          } as AudioDataEvent);
          break;
          
        case 'dialog_event':
          this.emit('dialogEvent', {
            type: data.data.eventType,
            data: data.data.data,
            timestamp: data.data.timestamp
          } as DialogEvent);
          break;
          
        case 'error':
          this.emit('error', {
            message: data.data.message,
            code: data.data.code,
            timestamp: data.data.timestamp
          });
          break;
          
        case 'connection_state':
          this.emit('connectionStateChanged', {
            oldState: data.data.oldState,
            newState: data.data.newState,
            timestamp: data.data.timestamp
          } as ConnectionStateEvent);
          break;
          
        default:
          console.warn('未知的WebSocket消息类型:', data.type);
      }
    } catch (error) {
      console.error('处理WebSocket消息失败:', error);
    }
  }

  /**
   * 发送音频数据
   */
  public sendAudioData(audioData: ArrayBuffer): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(audioData);
    } else {
      console.warn('WebSocket未连接，无法发送音频数据');
    }
  }

  // #endregion

  // #region Utility Methods

  /**
   * 检查WebSocket是否已连接
   */
  public isWebSocketConnected(): boolean {
    return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
  }

  /**
   * 获取当前连接状态
   */
  public getConnectionState(): RealTimeConnectionState {
    return this.connectionState;
  }

  /**
   * 清理资源
   */
  public dispose(): void {
    this.disconnectWebSocket();
    this.eventListeners.clear();
  }

  // #endregion

  // #region 兼容性方法（为了适配现有的前端代码）

  /**
   * 创建会话（兼容性方法）
   */
  public async createSession(request: any): Promise<any> {
    // 转换为新的连接请求格式
    const connectRequest: RealTimeConnectRequest = {
      appId: request.appId,
      accessToken: request.accessToken,
      botName: request.botName,
      systemRole: request.systemRole,
      greetingMessage: request.greetingMessage
    };
    
    await this.connect(connectRequest);
    return { sessionId: 'default' }; // 返回一个默认的sessionId
  }

  /**
   * 启动会话（兼容性方法）
   */
  public async startSession(sessionId: string, request: any): Promise<void> {
    await this.enableAudio();
    this.emit('sessionStarted', { sessionId });
  }

  /**
   * 结束会话（兼容性方法）
   */
  public async finishSession(sessionId: string): Promise<void> {
    await this.disableAudio();
    await this.disconnect();
    this.emit('sessionEnded', { sessionId });
  }

  /**
   * 发送问候语（兼容性方法）
   */
  public async sayHello(sessionId: string, request: any): Promise<void> {
    if (request.content) {
      await this.sendText(request.content);
    }
  }

  /**
   * 获取会话ID（兼容性方法）
   */
  public getSessionId(): string | null {
    return this.connectionState === RealTimeConnectionState.Connected ? 'default' : null;
  }

  // #endregion
}