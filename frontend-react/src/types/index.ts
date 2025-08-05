// 语音模式类型
export type VoiceMode = 'edge' | 'llm';
export type LLMProvider = 'openai' | 'doubao';

// LLM配置接口
export interface OpenAIConfig {
  baseUrl: string;
  apiKey: string;
  model: string;
  voice: string;
  // 音频控制参数
  speed?: number;
  pitch?: number;
  volume?: number;
}

export interface DoubaoConfig {
  appId: string;
  accessToken: string;
  cluster: string;
  endpoint: string;
  audioEncoding: string;
  // 音频控制参数
  speed?: number;
  pitch?: number;
  volume?: number;
}

export interface LLMSettings {
  openai?: OpenAIConfig;
  doubao?: DoubaoConfig;
}

// 统一的LLM配置接口
export interface LLMConfiguration {
  openai: OpenAIConfig;
  doubao: DoubaoConfig;
}

// 音频配置接口
export interface AudioConfig {
  // 基础配置
  volume: number;
  rate: number;
  pitch: number;
  
  // 语音模式配置
  voiceMode: VoiceMode;
  llmProvider: LLMProvider;
  
  // Edge语音配置
  selectedLanguage: string;
  selectedGender: 'All' | 'Male' | 'Female';
  selectedVoice: string;
  
  // LLM配置
  llmConfiguration: LLMConfiguration;
  
  // 文本相关
  inputText: string;
  previewText: string;
  previewAudioUrl: string;
  
  // 兼容性字段（保留以防向后兼容）
  superLong?: boolean;
}

// 语音信息接口
export interface Voice {
  Name: string;
  cnName?: string;
  Gender: 'Male' | 'Female';
  ContentCategories: string[];
  VoicePersonalities: string[];
}

// 音频项目接口
export interface AudioItem {
  audio: string;
  file: string;
  size?: number;
  srt?: string;
  isDownloading: boolean;
  isSrtLoading: boolean;
  isPlaying: boolean;
  progress: number;
  blobs?: Blob[];
  name?: string;
  download?: () => void;
  // 新增属性
  taskId?: string;
  text?: string;
  title?: string;
  status?: 'pending' | 'processing' | 'completed' | 'failed';
  error?: string;
  voice?: string;
  gender?: string;
  format?: string;
  audioUrl?: string;
}

// 生成状态接口
export interface GenerationState {
  audio: string | null;
  file: string | null;
  progress: number;
  audioList: AudioItem[];
}

// API请求接口
export interface GenerateRequest {
  text: string;
  voice?: string;
  rate?: string;
  pitch?: string;
  useLLM?: boolean;
  gender?: string;
}

// API响应接口
export interface GenerateResponse {
  success: boolean;
  data: {
    audio: string;
    file: string;
    srt?: string;
    size?: number;
    id: string;
  };
}

// 语音列表响应接口
export interface VoiceListResponse {
  success: boolean;
  data: Voice[];
}

// 任务接口
export interface Task {
  id: string;
  fields: any;
  status: string;
  progress: number;
  message: string;
  code?: string | number;
  result: any;
  createdAt: Date;
  updatedAt?: Date;
}

// 任务状态响应接口
export interface TaskStatusResponse {
  success: boolean;
  data: {
    progress: number;
    status: string;
    error?: string;
    result?: {
      audioUrl?: string;
      srtUrl?: string;
    };
  };
}

// 语音段落接口（用于演示播放器）
export interface VoiceSegment {
  voice: string;
  start: number;
  end: number;
  avatar: string;
  color: string;
  textColor: string;
}

// 语言选项接口
export interface LanguageOption {
  code: string;
  name: string;
}

// API错误响应接口
export interface ApiError {
  success: false;
  message: string;
  code?: string | number;
}

// 通用API响应类型
export type ApiResponse<T> = {
  success: true;
  data: T;
} | ApiError;

// 实时语音对话相关类型
export enum RealTimeConnectionState {
  Disconnected = 'disconnected',
  Connecting = 'connecting',
  Connected = 'connected',
  InSession = 'in_session',
  Disconnecting = 'disconnecting',
  Error = 'error'
}

export interface RealTimeConfig {
  webSocketUrl: string;
  appId: string;
  accessToken: string;
  connectionTimeoutMs?: number;
  heartbeatIntervalMs?: number;
  audioBufferSeconds?: number;
  inputSampleRate?: number;
  outputSampleRate?: number;
}

export interface RealTimeAudioConfig {
  channel: number;
  format: string;
  sampleRate: number;
}

export interface TtsConfig {
  audioConfig: RealTimeAudioConfig;
}

export interface DialogConfig {
  dialogId?: string;
  botName: string;
  systemRole: string;
  speakingStyle: string;
  extra: Record<string, any>;
}

export interface StartSessionPayload {
  tts: TtsConfig;
  dialog: DialogConfig;
}

export interface SayHelloPayload {
  content: string;
}

export interface ChatTtsTextPayload {
  start: boolean;
  end: boolean;
  content: string;
}

export interface RealTimeSession {
  sessionId: string;
  createdAt: Date;
  state: RealTimeConnectionState;
  lastActiveAt?: Date;
}

export interface AudioVisualizationData {
  frequencies: number[];
  volume: number;
  isRecording: boolean;
  isPlaying: boolean;
}

export interface RealTimeCallState {
  connectionState: RealTimeConnectionState;
  session: RealTimeSession | null;
  isRecording: boolean;
  isPlaying: boolean;
  audioVisualization: AudioVisualizationData;
  error: string | null;
  stats: Record<string, any>;
}

export interface RealTimeEventData {
  type: 'connection' | 'audio' | 'dialog' | 'error';
  data: any;
  timestamp: Date;
}

// WebSocket消息类型
export enum MessageType {
  FullClient = 'full_client',
  AudioOnlyClient = 'audio_only_client',
  FullServer = 'full_server',
  AudioOnlyServer = 'audio_only_server',
  Error = 'error'
}

export interface ProtocolMessage {
  type: MessageType;
  sequence: number;
  payload: Uint8Array;
}

export enum RealTimeEventType {
  StartSession = 'start_session',
  FinishSession = 'finish_session',
  SayHello = 'say_hello',
  ChatTtsText = 'chat_tts_text'
}