import axios from 'axios';

const DEV_URL = 'http://localhost:5000/api';
const PROD_URL = import.meta.env.VITE_API_URL || '/api';
const baseURL = import.meta.env.MODE === 'development' ? DEV_URL : PROD_URL;

const api = axios.create({
  baseURL: baseURL,
  timeout: 60000,
});

// 请求接口类型定义
export interface TtsRequest {
  text: string;
  voice?: string;
  speed?: number;
  pitch?: number;
  volume?: number;
  responseFormat?: 'mp3' | 'wav';
}

export interface LlmAnalysisRequest {
  text: string;
  options?: {
    useMultiEngine?: boolean;
    depth?: 'Basic' | 'Detailed' | 'Comprehensive';
  };
}

export interface VoiceRecommendationRequest {
  text: string;
  preferredEngine?: 'OpenAI' | 'Doubao' | 'Edge' | 'Kokoro';
}

export interface LlmConfiguration {
  modelType: 'OpenAI' | 'Doubao';
  modelName: string;
  endpoint: string;
  apiKey: string;
  enabled: boolean;
}

// 响应接口类型定义
export interface TtsResponse {
  isSuccess: boolean;
  audioData?: string;
  fileName?: string;
  errorMessage?: string;
  engineUsed?: string;
}

export interface LlmAnalysisResult {
  detectedLanguage: string;
  emotionTone: string;
  recommendedEngine: string;
  recommendedVoice: string;
  confidence: number;
  segments: Array<{
    text: string;
    startIndex: number;
    endIndex: number;
    type: string;
  }>;
}

export interface VoiceRecommendation {
  recommendedEngine: string;
  recommendedVoice: string;
  reason: string;
  confidence: number;
  alternatives: VoiceRecommendation[];
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
}

// API 服务类
export class EasyVoiceApi {
  // TTS 相关 API
  static async generateTts(request: TtsRequest): Promise<ApiResponse<TtsResponse>> {
    try {
      const response = await api.post<ApiResponse<TtsResponse>>('/tts/generate', request);
      return response.data;
    } catch (error) {
      console.error('TTS generation failed:', error);
      throw error;
    }
  }

  static async generateIntelligentTts(request: TtsRequest & { useLlm?: boolean }): Promise<ApiResponse<TtsResponse>> {
    try {
      const response = await api.post<ApiResponse<TtsResponse>>('/tts/intelligent', request);
      return response.data;
    } catch (error) {
      console.error('Intelligent TTS generation failed:', error);
      throw error;
    }
  }

  // LLM 分析相关 API
  static async analyzeText(request: LlmAnalysisRequest): Promise<ApiResponse<LlmAnalysisResult>> {
    try {
      const response = await api.post<ApiResponse<LlmAnalysisResult>>('/llm/analyze', request);
      return response.data;
    } catch (error) {
      console.error('Text analysis failed:', error);
      throw error;
    }
  }

  static async recommendVoice(request: VoiceRecommendationRequest): Promise<ApiResponse<VoiceRecommendation>> {
    try {
      const response = await api.post<ApiResponse<VoiceRecommendation>>('/llm/recommend-voice', request);
      return response.data;
    } catch (error) {
      console.error('Voice recommendation failed:', error);
      throw error;
    }
  }

  // LLM 配置相关 API
  static async getLlmConfigurations(): Promise<ApiResponse<LlmConfiguration[]>> {
    try {
      const response = await api.get<ApiResponse<LlmConfiguration[]>>('/llm/configurations');
      return response.data;
    } catch (error) {
      console.error('Get LLM configurations failed:', error);
      throw error;
    }
  }

  static async updateLlmConfiguration(config: LlmConfiguration): Promise<ApiResponse<boolean>> {
    try {
      const response = await api.put<ApiResponse<boolean>>('/llm/configuration', config);
      return response.data;
    } catch (error) {
      console.error('Update LLM configuration failed:', error);
      throw error;
    }
  }

  static async testLlmConfiguration(config: LlmConfiguration): Promise<ApiResponse<boolean>> {
    try {
      const response = await api.post<ApiResponse<boolean>>('/llm/test-configuration', config);
      return response.data;
    } catch (error) {
      console.error('Test LLM configuration failed:', error);
      throw error;
    }
  }

  // 获取支持的语音列表
  static async getVoiceList(): Promise<ApiResponse<Array<{ name: string; displayName: string; gender: string; language: string }>>> {
    try {
      const response = await api.get<ApiResponse<Array<{ name: string; displayName: string; gender: string; language: string }>>>('/tts/voices');
      return response.data;
    } catch (error) {
      console.error('Get voice list failed:', error);
      throw error;
    }
  }
}

export default api;