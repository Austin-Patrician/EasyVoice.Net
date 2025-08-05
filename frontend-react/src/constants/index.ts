import type { LanguageOption, AudioConfig } from '../types';

// 支持的语言列表
export const LANGUAGES: LanguageOption[] = [
  { code: 'zh-CN', name: '中文（普通话）' },
  { code: 'en-US', name: 'English (US)' },
  { code: 'ja-JP', name: '日本語' },
  { code: 'ko-KR', name: '한국어' },
  { code: 'fr-FR', name: 'Français' },
  { code: 'de-DE', name: 'Deutsch' },
  { code: 'es-ES', name: 'Español' },
  { code: 'it-IT', name: 'Italiano' },
  { code: 'pt-BR', name: 'Português (Brasil)' },
  { code: 'ru-RU', name: 'Русский' },
];

// 性别选项
export const GENDER_OPTIONS = [
  { value: 'All', label: '全部' },
  { value: 'Male', label: '男性' },
  { value: 'Female', label: '女性' },
] as const;

// 语音模式选项
export const VOICE_MODE_OPTIONS = [
  { value: 'edge', label: 'Edge 语音' },
  { value: 'llm', label: 'LLM 语音' },
] as const;

// LLM提供商选项
export const LLM_PROVIDER_OPTIONS = [
  { value: 'openai', label: 'OpenAI' },
  { value: 'doubao', label: '豆包' },
] as const;

// 默认音频配置
export const DEFAULT_AUDIO_CONFIG: AudioConfig = {
  // 基础配置
  volume: 0.5,
  rate: 1.0,
  pitch: 1.0,
  
  // 语音模式配置
  voiceMode: 'edge',
  llmProvider: 'openai',
  
  // Edge语音配置
  selectedLanguage: 'zh-CN',
  selectedGender: 'All',
  selectedVoice: '',
  
  // LLM配置
  llmConfiguration: {
    openai: {
      baseUrl: 'https://api.openai.com/v1',
      apiKey: '',
      model: 'tts-1',
      voice: 'alloy',
      speed: 1.0,
      pitch: 1.0,
      volume: 0.5,
    },
    doubao: {
      appId: '',
      accessToken: '',
      cluster: '',
      endpoint: '',
      audioEncoding: 'mp3',
      speed: 1.0,
      pitch: 1.0,
      volume: 0.5,
    },
  },
  
  // 文本相关
  inputText: '',
  previewText: '这是一个语音预览示例。',
  previewAudioUrl: '',
  
  // 兼容性字段（保留以防向后兼容）
  superLong: false,
};

// API端点
export const API_ENDPOINTS = {
  GENERATE: '/api/generate',
  VOICES: '/api/voices',
  TASK_STATUS: '/api/task',
  DOWNLOAD: '/api/download',
  // LLM语音端点
  OPENAI_TTS: '/api/openai-tts',
  DOUBAO_TTS: '/api/doubao-tts',
  // 实时语音端点
  REALTIME_CONNECT: '/api/realtime/connect',
  REALTIME_DISCONNECT: '/api/realtime/disconnect',
  REALTIME_START_SESSION: '/api/realtime/start-session',
  REALTIME_FINISH_SESSION: '/api/realtime/finish-session',
  REALTIME_DEMO: '/api/realtime/demo',
} as const;

// 音频格式
export const AUDIO_FORMATS = {
  MP3: 'audio/mpeg',
  WAV: 'audio/wav',
  OGG: 'audio/ogg',
} as const;

// 最大文本长度
export const MAX_TEXT_LENGTH = 10000;

// 预览文本长度限制
export const PREVIEW_TEXT_LENGTH = 100;

// 轮询间隔（毫秒）
export const POLLING_INTERVAL = 1000;

// 下载超时时间（毫秒）
export const DOWNLOAD_TIMEOUT = 30000;

// 支持的文件类型
export const SUPPORTED_FILE_TYPES = ['.txt', '.docx', '.pdf'] as const;

// 错误消息
export const ERROR_MESSAGES = {
  NETWORK_ERROR: '网络连接失败，请检查网络设置',
  INVALID_TEXT: '请输入有效的文本内容',
  TEXT_TOO_LONG: `文本长度不能超过 ${MAX_TEXT_LENGTH} 个字符`,
  VOICE_NOT_SELECTED: '请选择一个语音',
  GENERATION_FAILED: '语音生成失败，请重试',
  DOWNLOAD_FAILED: '下载失败，请重试',
  INVALID_API_KEY: 'API密钥无效',
  RATE_LIMIT_EXCEEDED: '请求频率过高，请稍后重试',
} as const;

// 成功消息
export const SUCCESS_MESSAGES = {
  GENERATION_COMPLETE: '语音生成完成',
  DOWNLOAD_COMPLETE: '下载完成',
  PREVIEW_READY: '预览准备就绪',
  REALTIME_CONNECTED: '实时语音连接成功',
  REALTIME_SESSION_STARTED: '语音会话已开始',
  REALTIME_SESSION_ENDED: '语音会话已结束',
} as const;

// 实时语音配置
export const REALTIME_CONFIG = {
  // WebSocket配置
  WEBSOCKET_URL: 'wss://openspeech.bytedance.com/api/v3/realtime/dialogue',
  CONNECTION_TIMEOUT: 30000,
  HEARTBEAT_INTERVAL: 30000,
  
  // 音频配置
  AUDIO_BUFFER_SECONDS: 100,
  INPUT_SAMPLE_RATE: 16000,
  OUTPUT_SAMPLE_RATE: 24000,
  AUDIO_FRAME_SIZE: 160,
  
  // 默认对话配置
  DEFAULT_DIALOG: {
    botName: '豆包',
    systemRole: '你使用活泼灵动的女声，性格开朗，热爱生活。',
    speakingStyle: '你的说话风格简洁明了，语速适中，语调自然。',
    extra: {
      strict_audit: false,
      audit_response: '抱歉这个问题我无法回答，你可以换个其他话题，我会尽力为你提供帮助。'
    }
  },
  
  // 默认TTS配置
  DEFAULT_TTS: {
    audioConfig: {
      channel: 1,
      format: 'pcm',
      sampleRate: 24000
    }
  },
  
  // 音频可视化配置
  VISUALIZATION: {
    FFT_SIZE: 256,
    SMOOTHING_TIME_CONSTANT: 0.8,
    MIN_DECIBELS: -90,
    MAX_DECIBELS: -10,
    UPDATE_INTERVAL: 16 // 60fps
  }
} as const;

// 实时语音错误消息
export const REALTIME_ERROR_MESSAGES = {
  CONNECTION_FAILED: '连接实时语音服务失败',
  WEBSOCKET_ERROR: 'WebSocket连接错误',
  AUDIO_PERMISSION_DENIED: '麦克风权限被拒绝',
  AUDIO_DEVICE_ERROR: '音频设备错误',
  SESSION_START_FAILED: '开始语音会话失败',
  SESSION_END_FAILED: '结束语音会话失败',
  INVALID_CONFIG: '配置参数无效',
  NETWORK_ERROR: '网络连接错误',
} as const;