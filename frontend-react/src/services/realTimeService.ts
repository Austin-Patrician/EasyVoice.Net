import { 
  RealTimeConfig, 
  RealTimeConnectionState, 
  StartSessionPayload, 
  SayHelloPayload, 
  ChatTtsTextPayload,
  MessageType,
  RealTimeEventType
} from '../types';

interface WebSocketMessage {
  type: MessageType;
  payload?: any;
  timestamp: number;
}
import { REALTIME_CONFIG, REALTIME_ERROR_MESSAGES } from '../constants';

export class RealTimeService {
  private ws: WebSocket | null = null;
  private connectionState: RealTimeConnectionState = RealTimeConnectionState.Disconnected;
  private config: RealTimeConfig;
  private heartbeatInterval: NodeJS.Timeout | null = null;
  private connectionTimeout: NodeJS.Timeout | null = null;
  private messageQueue: WebSocketMessage[] = [];
  private eventListeners: Map<string, Function[]> = new Map();
  private sessionId: string | null = null;
  private connectId: string;

  constructor(config: RealTimeConfig) {
    this.config = config;
    this.connectId = this.generateConnectId();
    this.initializeEventListeners();
  }

  private generateConnectId(): string {
    return `connect_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  private initializeEventListeners(): void {
    this.eventListeners.set('connectionStateChange', []);
    this.eventListeners.set('message', []);
    this.eventListeners.set('error', []);
    this.eventListeners.set('audioData', []);
    this.eventListeners.set('sessionStarted', []);
    this.eventListeners.set('sessionEnded', []);
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
        console.error(`Error in event listener for ${event}:`, error);
      }
    });
  }

  private setConnectionState(state: RealTimeConnectionState): void {
    if (this.connectionState !== state) {
      this.connectionState = state;
      this.emit('connectionStateChange', state);
    }
  }

  public getConnectionState(): RealTimeConnectionState {
    return this.connectionState;
  }

  public async connect(): Promise<void> {
    if (this.connectionState === RealTimeConnectionState.Connecting || 
        this.connectionState === RealTimeConnectionState.Connected) {
      return;
    }

    this.setConnectionState(RealTimeConnectionState.Connecting);

    try {
      // 构建WebSocket URL和headers
      const wsUrl = new URL('wss://openspeech.bytedance.com/api/v3/realtime/dialogue');
      
      // 创建WebSocket连接
      this.ws = new WebSocket(wsUrl.toString());
      
      // 设置连接超时
      this.connectionTimeout = setTimeout(() => {
        if (this.connectionState === RealTimeConnectionState.Connecting) {
          this.disconnect();
          this.emit('error', new Error(REALTIME_ERROR_MESSAGES.CONNECTION_FAILED));
        }
      }, REALTIME_CONFIG.CONNECTION_TIMEOUT);

      // 设置WebSocket事件处理器
      this.setupWebSocketHandlers();

    } catch (error) {
      this.setConnectionState(RealTimeConnectionState.Disconnected);
      this.emit('error', error);
      throw error;
    }
  }

  private setupWebSocketHandlers(): void {
    if (!this.ws) return;

    this.ws.onopen = () => {
      console.log('WebSocket连接已建立');
      this.setConnectionState(RealTimeConnectionState.Connected);
      
      if (this.connectionTimeout) {
        clearTimeout(this.connectionTimeout);
        this.connectionTimeout = null;
      }

      // 开始心跳
      this.startHeartbeat();
      
      // 发送队列中的消息
      this.flushMessageQueue();
    };

    this.ws.onmessage = (event) => {
      this.handleMessage(event);
    };

    this.ws.onerror = (error) => {
      console.error('WebSocket错误:', error);
      this.emit('error', new Error(REALTIME_ERROR_MESSAGES.WEBSOCKET_ERROR));
    };

    this.ws.onclose = (event) => {
      console.log('WebSocket连接已关闭:', event.code, event.reason);
      this.setConnectionState(RealTimeConnectionState.Disconnected);
      this.cleanup();
    };
  }

  private handleMessage(event: MessageEvent): void {
    try {
      if (event.data instanceof ArrayBuffer) {
        // 处理二进制音频数据
        this.emit('audioData', event.data);
      } else {
        // 处理JSON消息
        const message = JSON.parse(event.data);
        this.emit('message', message);
        
        // 处理特定消息类型
        this.handleSpecificMessage(message);
      }
    } catch (error) {
      console.error('处理WebSocket消息时出错:', error);
      this.emit('error', error);
    }
  }

  private handleSpecificMessage(message: any): void {
    switch (message.type) {
      case 'session_started':
        this.sessionId = message.session_id;
        this.emit('sessionStarted', message);
        break;
      case 'session_ended':
        this.sessionId = null;
        this.emit('sessionEnded', message);
        break;
      case 'error':
        this.emit('error', new Error(message.message || '服务器错误'));
        break;
      default:
        // 其他消息类型的处理
        break;
    }
  }

  private startHeartbeat(): void {
    this.heartbeatInterval = setInterval(() => {
      if (this.ws && this.ws.readyState === WebSocket.OPEN) {
        this.sendMessage({
          type: MessageType.FullClient,
          timestamp: Date.now()
        });
      }
    }, REALTIME_CONFIG.HEARTBEAT_INTERVAL);
  }

  private flushMessageQueue(): void {
    while (this.messageQueue.length > 0) {
      const message = this.messageQueue.shift();
      if (message) {
        this.sendMessage(message);
      }
    }
  }

  public sendMessage(message: WebSocketMessage): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(message));
    } else {
      // 如果连接未就绪，将消息加入队列
      this.messageQueue.push(message);
    }
  }

  public sendBinaryData(data: ArrayBuffer): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(data);
    }
  }

  public async startSession(payload: StartSessionPayload): Promise<void> {
    if (this.connectionState !== RealTimeConnectionState.Connected) {
      throw new Error('WebSocket未连接');
    }

    const message: WebSocketMessage = {
      type: MessageType.FullClient,
      payload,
      timestamp: Date.now()
    };

    this.sendMessage(message);
  }

  public async finishSession(): Promise<void> {
    if (!this.sessionId) {
      throw new Error('没有活动的会话');
    }

    const message: WebSocketMessage = {
      type: MessageType.FullClient,
      payload: { session_id: this.sessionId },
      timestamp: Date.now()
    };

    this.sendMessage(message);
  }

  public async sayHello(payload: SayHelloPayload): Promise<void> {
    const message: WebSocketMessage = {
      type: MessageType.FullClient,
      payload,
      timestamp: Date.now()
    };

    this.sendMessage(message);
  }

  public async sendChatTtsText(payload: ChatTtsTextPayload): Promise<void> {
    const message: WebSocketMessage = {
      type: MessageType.FullClient,
      payload,
      timestamp: Date.now()
    };

    this.sendMessage(message);
  }

  public disconnect(): void {
    this.setConnectionState(RealTimeConnectionState.Disconnecting);
    
    if (this.ws) {
      this.ws.close(1000, '正常关闭');
    }
    
    this.cleanup();
  }

  private cleanup(): void {
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
      this.heartbeatInterval = null;
    }

    if (this.connectionTimeout) {
      clearTimeout(this.connectionTimeout);
      this.connectionTimeout = null;
    }

    this.messageQueue = [];
    this.sessionId = null;
    this.ws = null;
    
    this.setConnectionState(RealTimeConnectionState.Disconnected);
  }

  public getSessionId(): string | null {
    return this.sessionId;
  }

  public getConnectId(): string {
    return this.connectId;
  }

  public isConnected(): boolean {
    return this.connectionState === RealTimeConnectionState.Connected;
  }

  public destroy(): void {
    this.disconnect();
    this.eventListeners.clear();
  }
}

// 创建默认实例的工厂函数
export function createRealTimeService(config?: Partial<RealTimeConfig>): RealTimeService {
  const defaultConfig: RealTimeConfig = {
    webSocketUrl: 'wss://openspeech.bytedance.com/api/v3/realtime/dialogue',
    appId: '', // 需要从环境变量或配置中获取
    accessToken: '', // 需要从环境变量或配置中获取
    inputSampleRate: REALTIME_CONFIG.INPUT_SAMPLE_RATE,
    outputSampleRate: REALTIME_CONFIG.OUTPUT_SAMPLE_RATE,
    connectionTimeoutMs: 30000,
    heartbeatIntervalMs: 30000
  };

  const mergedConfig = { ...defaultConfig, ...config };
  return new RealTimeService(mergedConfig);
}