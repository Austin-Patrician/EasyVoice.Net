/**
 * 配置管理服务
 * 用于管理应用程序的配置信息，包括API密钥、端点等敏感信息
 */

export interface AppConfig {
  /** 豆包应用ID */
  appId: string;
  /** 豆包访问令牌 */
  accessToken: string;
  /** WebSocket连接URL */
  webSocketUrl: string;
  /** API基础URL */
  apiBaseUrl: string;
  /** 连接超时时间（毫秒） */
  connectionTimeoutMs: number;
  /** 音频缓冲时间（秒） */
  audioBufferSeconds: number;
  /** 机器人名称 */
  botName: string;
  /** 系统角色设定 */
  systemRole: string;
  /** 说话风格 */
  speakingStyle: string;
  /** 问候语 */
  greetingMessage: string;
}

export interface AudioConfig {
  /** 默认采样率 */
  defaultSampleRate: number;
  /** 默认声道数 */
  defaultChannels: number;
  /** 默认音频格式 */
  defaultFormat: string;
  /** 音频块大小 */
  chunkSize: number;
}

export interface UIConfig {
  /** 是否启用调试模式 */
  debugMode: boolean;
  /** 主题设置 */
  theme: 'light' | 'dark' | 'auto';
  /** 语言设置 */
  language: 'zh-CN' | 'en-US';
}

export interface FullConfig {
  app: AppConfig;
  audio: AudioConfig;
  ui: UIConfig;
}

class ConfigService {
  private config: FullConfig | null = null;
  private readonly CONFIG_STORAGE_KEY = 'easyvoice_config';

  /**
   * 初始化配置服务
   */
  async initialize(): Promise<void> {
    try {
      // 1. 尝试从环境变量加载
      const envConfig = this.loadFromEnvironment();
      
      // 2. 尝试从本地存储加载
      const storageConfig = this.loadFromStorage();
      
      // 3. 合并配置（环境变量优先）
      this.config = this.mergeConfigs(this.getDefaultConfig(), storageConfig, envConfig);
      
      // 4. 验证配置
      this.validateConfig(this.config);
      
      console.log('配置服务初始化成功');
    } catch (error) {
      console.error('配置服务初始化失败:', error);
      // 使用默认配置作为后备
      this.config = this.getDefaultConfig();
    }
  }

  /**
   * 获取应用配置
   */
  getAppConfig(): AppConfig {
    if (!this.config) {
      throw new Error('配置服务未初始化，请先调用 initialize()');
    }
    return this.config.app;
  }

  /**
   * 获取音频配置
   */
  getAudioConfig(): AudioConfig {
    if (!this.config) {
      throw new Error('配置服务未初始化，请先调用 initialize()');
    }
    return this.config.audio;
  }

  /**
   * 获取UI配置
   */
  getUIConfig(): UIConfig {
    if (!this.config) {
      throw new Error('配置服务未初始化，请先调用 initialize()');
    }
    return this.config.ui;
  }

  /**
   * 获取完整配置（为了向后兼容）
   */
  getConfig(): FullConfig {
    if (!this.config) {
      throw new Error('配置服务未初始化，请先调用 initialize()');
    }
    return this.config;
  }

  /**
   * 更新配置
   */
  updateConfig(updates: Partial<FullConfig>): void {
    if (!this.config) {
      throw new Error('配置服务未初始化');
    }
    
    this.config = this.mergeConfigs(this.config, updates);
    this.saveToStorage(this.config);
  }

  /**
   * 重置为默认配置
   */
  resetToDefault(): void {
    this.config = this.getDefaultConfig();
    this.saveToStorage(this.config);
  }

  /**
   * 从环境变量加载配置
   */
  private loadFromEnvironment(): Partial<FullConfig> {
    const env = import.meta.env;
    
    return {
      app: {
        appId: env.VITE_DOUBAO_APP_ID || '',
        accessToken: env.VITE_DOUBAO_ACCESS_TOKEN || '',
        webSocketUrl: env.VITE_WEBSOCKET_URL || 'wss://openspeech.bytedance.com/api/v3/realtime/dialogue',
        apiBaseUrl: env.VITE_API_BASE_URL || 'http://localhost:5094',
        connectionTimeoutMs: parseInt(env.VITE_CONNECTION_TIMEOUT_MS || '30000'),
        audioBufferSeconds: parseInt(env.VITE_AUDIO_BUFFER_SECONDS || '100'),
        botName: env.VITE_BOT_NAME || '豆包',
        systemRole: env.VITE_SYSTEM_ROLE || '你使用活泼灵动的女声，性格开朗，热爱生活。',
        speakingStyle: env.VITE_SPEAKING_STYLE || '你的说话风格简洁明了，语速适中，语调自然。',
        greetingMessage: env.VITE_GREETING_MESSAGE || '你好，我想开始语音对话。'
      },
      ui: {
        debugMode: env.VITE_DEBUG_MODE === 'true',
        theme: (env.VITE_THEME as 'light' | 'dark' | 'auto') || 'auto',
        language: (env.VITE_LANGUAGE as 'zh-CN' | 'en-US') || 'zh-CN'
      }
    };
  }

  /**
   * 从本地存储加载配置
   */
  private loadFromStorage(): Partial<FullConfig> {
    try {
      const stored = localStorage.getItem(this.CONFIG_STORAGE_KEY);
      return stored ? JSON.parse(stored) : {};
    } catch (error) {
      console.warn('从本地存储加载配置失败:', error);
      return {};
    }
  }

  /**
   * 保存配置到本地存储
   */
  private saveToStorage(config: FullConfig): void {
    try {
      // 不保存敏感信息到本地存储
      const configToSave = {
        ...config,
        app: {
          ...config.app,
          appId: '', // 不保存敏感信息
          accessToken: '' // 不保存敏感信息
        }
      };
      localStorage.setItem(this.CONFIG_STORAGE_KEY, JSON.stringify(configToSave));
    } catch (error) {
      console.warn('保存配置到本地存储失败:', error);
    }
  }

  /**
   * 获取默认配置
   */
  private getDefaultConfig(): FullConfig {
    return {
      app: {
        appId: '',
        accessToken: '',
        webSocketUrl: 'wss://openspeech.bytedance.com/api/v3/realtime/dialogue',
        apiBaseUrl: 'http://localhost:5094',
        connectionTimeoutMs: 30000,
        audioBufferSeconds: 100,
        botName: '豆包',
        systemRole: '你使用活泼灵动的女声，性格开朗，热爱生活。',
        speakingStyle: '你的说话风格简洁明了，语速适中，语调自然。',
        greetingMessage: '你好，我想开始语音对话。'
      },
      audio: {
        defaultSampleRate: 24000,
        defaultChannels: 1,
        defaultFormat: 'pcm',
        chunkSize: 1024
      },
      ui: {
        debugMode: false,
        theme: 'auto',
        language: 'zh-CN'
      }
    };
  }

  /**
   * 合并配置对象
   */
  private mergeConfigs(...configs: Partial<FullConfig>[]): FullConfig {
    const result = this.getDefaultConfig();
    
    for (const config of configs) {
      if (config.app) {
        Object.assign(result.app, config.app);
      }
      if (config.audio) {
        Object.assign(result.audio, config.audio);
      }
      if (config.ui) {
        Object.assign(result.ui, config.ui);
      }
    }
    
    return result;
  }

  /**
   * 验证配置
   */
  private validateConfig(config: FullConfig): void {
    const { app } = config;
    
    if (!app.apiBaseUrl) {
      throw new Error('API基础URL不能为空');
    }
    
    if (!app.webSocketUrl) {
      throw new Error('WebSocket URL不能为空');
    }
    
    if (app.connectionTimeoutMs <= 0) {
      throw new Error('连接超时时间必须大于0');
    }
    
    if (app.audioBufferSeconds <= 0) {
      throw new Error('音频缓冲时间必须大于0');
    }
    
    // 在生产环境中检查必需的敏感配置
    if (import.meta.env.PROD) {
      if (!app.appId) {
        console.warn('警告: 豆包应用ID未配置，某些功能可能无法正常工作');
      }
      
      if (!app.accessToken) {
        console.warn('警告: 豆包访问令牌未配置，某些功能可能无法正常工作');
      }
    }
  }

  /**
   * 检查配置是否完整
   */
  isConfigComplete(): boolean {
    if (!this.config) return false;
    
    const { app } = this.config;
    return !!(app.appId && app.accessToken && app.apiBaseUrl && app.webSocketUrl);
  }

  /**
   * 获取配置状态
   */
  getConfigStatus(): {
    initialized: boolean;
    complete: boolean;
    missingFields: string[];
  } {
    const initialized = !!this.config;
    const complete = this.isConfigComplete();
    const missingFields: string[] = [];
    
    if (this.config) {
      const { app } = this.config;
      if (!app.appId) missingFields.push('appId');
      if (!app.accessToken) missingFields.push('accessToken');
      if (!app.apiBaseUrl) missingFields.push('apiBaseUrl');
      if (!app.webSocketUrl) missingFields.push('webSocketUrl');
    }
    
    return { initialized, complete, missingFields };
  }
}

// 导出单例实例
export const configService = new ConfigService();

// 导出类型
// 由于 FullConfig 已在上方通过 interface 声明导出，此处无需重复导出
