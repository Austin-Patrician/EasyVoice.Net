import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { LlmConfiguration } from '../services/api';

// TTS 配置接口
export interface TtsConfig {
  voice: string;
  speed: number;
  pitch: number;
  volume: number;
  responseFormat: 'mp3' | 'wav';
  language: string;
}

// LLM 配置接口
export interface LlmConfig {
  provider: 'openai' | 'doubao';
  apiUrl: string;
  apiKey: string;
  model: string;
  previewText: string;
}

// 应用配置接口
export interface AppConfig {
  // TTS 设置
  ttsConfig: TtsConfig;
  
  // LLM 设置
  llmConfigurations: LlmConfiguration[];
  activeLlmConfig?: LlmConfiguration;
  llmConfig?: LlmConfig;
  
  // UI 设置
  theme: 'light' | 'dark';
  language: 'zh-CN' | 'en-US';
  
  // 功能开关
  enableLlmAnalysis: boolean;
  enableVoiceRecommendation: boolean;
  useEdgeTtsAsDefault: boolean;
}

// 应用状态接口
export interface AppState {
  // 配置
  config: AppConfig;
  
  // 当前状态
  inputText: string;
  isGenerating: boolean;
  isAnalyzing: boolean;
  
  // 分析结果
  analysisResult?: {
    detectedLanguage: string;
    emotionTone: string;
    recommendedEngine: string;
    recommendedVoice: string;
    confidence: number;
  };
  
  // 语音推荐结果
  voiceRecommendation?: {
    recommendedEngine: string;
    recommendedVoice: string;
    reason: string;
    confidence: number;
  };
  
  // 生成结果
  generationResult?: {
    audioData: string;
    fileName: string;
    engineUsed: string;
  };
  
  // 错误信息
  error?: string;
}

// 默认配置
const defaultConfig: AppConfig = {
  ttsConfig: {
    voice: 'zh-CN-YunxiNeural',
    speed: 1.0,
    pitch: 0,
    volume: 0,
    responseFormat: 'mp3',
    language: 'zh-CN',
  },
  llmConfigurations: [],
  llmConfig: {
    provider: 'openai',
    apiUrl: '',
    apiKey: '',
    model: '',
    previewText: '这是一段测试文本',
  },
  theme: 'light',
  language: 'zh-CN',
  enableLlmAnalysis: true,
  enableVoiceRecommendation: true,
  useEdgeTtsAsDefault: true,
};

// 应用状态管理
export interface AppStore extends AppState {
  // 配置更新方法
  updateTtsConfig: (config: Partial<TtsConfig>) => void;
  updateLlmConfig: (config: Partial<LlmConfig>) => void;
  updateLlmConfigurations: (configs: LlmConfiguration[]) => void;
  setActiveLlmConfig: (config: LlmConfiguration) => void;
  updateAppSettings: (settings: Partial<Pick<AppConfig, 'theme' | 'language' | 'enableLlmAnalysis' | 'enableVoiceRecommendation' | 'useEdgeTtsAsDefault'>>) => void;
  
  // 文本和状态更新方法
  setInputText: (text: string) => void;
  setIsGenerating: (generating: boolean) => void;
  setIsAnalyzing: (analyzing: boolean) => void;
  
  // 结果更新方法
  setAnalysisResult: (result: AppState['analysisResult']) => void;
  setVoiceRecommendation: (recommendation: AppState['voiceRecommendation']) => void;
  setGenerationResult: (result: AppState['generationResult']) => void;
  
  // 错误处理
  setError: (error: string | undefined) => void;
  clearError: () => void;
  
  // 重置方法
  resetResults: () => void;
  resetAll: () => void;
}

export const useAppStore = create<AppStore>()(persist(
  (set, get) => ({
    // 初始状态
    config: defaultConfig,
    inputText: '',
    isGenerating: false,
    isAnalyzing: false,
    
    // 配置更新方法
    updateTtsConfig: (newConfig) => {
      set((state) => ({
        config: {
          ...state.config,
          ttsConfig: {
            ...state.config.ttsConfig,
            ...newConfig,
          },
        },
      }));
    },
    
    updateLlmConfig: (newConfig) => {
      set((state) => ({
        config: {
          ...state.config,
          llmConfig: {
            ...state.config.llmConfig!,
            ...newConfig,
          },
        },
      }));
    },
    
    updateLlmConfigurations: (configs) => {
      set((state) => ({
        config: {
          ...state.config,
          llmConfigurations: configs,
        },
      }));
    },
    
    setActiveLlmConfig: (config) => {
      set((state) => ({
        config: {
          ...state.config,
          activeLlmConfig: config,
        },
      }));
    },
    
    updateAppSettings: (settings) => {
      set((state) => ({
        config: {
          ...state.config,
          ...settings,
        },
      }));
    },
    
    // 文本和状态更新方法
    setInputText: (text) => {
      set({ inputText: text });
    },
    
    setIsGenerating: (generating) => {
      set({ isGenerating: generating });
    },
    
    setIsAnalyzing: (analyzing) => {
      set({ isAnalyzing: analyzing });
    },
    
    // 结果更新方法
    setAnalysisResult: (result) => {
      set({ analysisResult: result });
    },
    
    setVoiceRecommendation: (recommendation) => {
      set({ voiceRecommendation: recommendation });
    },
    
    setGenerationResult: (result) => {
      set({ generationResult: result });
    },
    
    // 错误处理
    setError: (error) => {
      set({ error });
    },
    
    clearError: () => {
      set({ error: undefined });
    },
    
    // 重置方法
    resetResults: () => {
      set({
        analysisResult: undefined,
        voiceRecommendation: undefined,
        generationResult: undefined,
        error: undefined,
      });
    },
    
    resetAll: () => {
      set({
        config: defaultConfig,
        inputText: '',
        isGenerating: false,
        isAnalyzing: false,
        analysisResult: undefined,
        voiceRecommendation: undefined,
        generationResult: undefined,
        error: undefined,
      });
    },
  }),
  {
    name: 'easy-voice-app-store',
    partialize: (state) => ({
      config: state.config,
      inputText: state.inputText,
    }),
  }
));