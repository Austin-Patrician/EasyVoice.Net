import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AudioConfig, Voice, VoiceMode, LLMProvider, OpenAIConfig, DoubaoConfig } from '../types';
import { DEFAULT_AUDIO_CONFIG } from '../constants';

interface AudioConfigState {
  // 音频配置
  config: AudioConfig;
  
  // 语音列表
  voices: Voice[];
  filteredVoices: Voice[];
  
  // 加载状态
  isLoadingVoices: boolean;
  isGenerating: boolean;
  isPreviewPlaying: boolean;
  
  // 错误状态
  error: string | null;
  
  // Actions
  updateConfig: (updates: Partial<AudioConfig>) => void;
  setVoices: (voices: Voice[]) => void;
  filterVoices: (language: string, gender: string) => void;
  setLoadingVoices: (loading: boolean) => void;
  setGenerating: (generating: boolean) => void;
  setPreviewPlaying: (playing: boolean) => void;
  setError: (error: string | null) => void;
  resetConfig: () => void;
  
  // 预览相关
  updatePreviewText: (text: string) => void;
  updatePreviewAudioUrl: (url: string) => void;
  
  // LLM语音配置
  updateLLMConfiguration: (provider: LLMProvider, config: OpenAIConfig | DoubaoConfig) => void;
  updateVoiceMode: (mode: VoiceMode) => void;
  updateLLMProvider: (provider: LLMProvider) => void;
}

export const useAudioConfigStore = create<AudioConfigState>()(
  persist(
    (set, get) => ({
      // 初始状态
      config: DEFAULT_AUDIO_CONFIG,
      voices: [],
      filteredVoices: [],
      isLoadingVoices: false,
      isGenerating: false,
      isPreviewPlaying: false,
      error: null,
      
      // 更新配置
      updateConfig: (updates) => {
        set((state) => ({
          config: { ...state.config, ...updates },
          error: null,
        }));
      },
      
      // 设置语音列表
      setVoices: (voices) => {
        set({ voices });
        // 自动过滤语音
        const { config } = get();
        get().filterVoices(config.selectedLanguage, config.selectedGender);
      },
      
      // 过滤语音
      filterVoices: (language, gender) => {
        const { voices } = get();
        let filtered = voices;
        
        // 按语言过滤（这里简化处理，实际可能需要更复杂的语言匹配逻辑）
        if (language && language !== 'zh-CN') {
          // 对于非中文语言，可能需要特殊处理
          filtered = voices.filter(voice => 
            voice.Name.toLowerCase().includes(language.split('-')[0])
          );
        }
        
        // 按性别过滤
        if (gender && gender !== 'All') {
          filtered = filtered.filter(voice => voice.Gender === gender);
        }
        
        set({ filteredVoices: filtered });
      },
      
      // 设置加载状态
      setLoadingVoices: (loading) => {
        set({ isLoadingVoices: loading });
      },
      
      // 设置生成状态
      setGenerating: (generating) => {
        set({ isGenerating: generating });
      },
      
      // 设置预览播放状态
      setPreviewPlaying: (playing) => {
        set({ isPreviewPlaying: playing });
      },
      
      // 设置错误
      setError: (error) => {
        set({ error });
      },
      
      // 重置配置
      resetConfig: () => {
        set({ 
          config: DEFAULT_AUDIO_CONFIG,
          error: null,
        });
      },
      
      // 更新预览文本
      updatePreviewText: (text) => {
        set((state) => ({
          config: { ...state.config, previewText: text },
        }));
      },
      
      // 更新预览音频URL
      updatePreviewAudioUrl: (url) => {
        set((state) => ({
          config: { ...state.config, previewAudioUrl: url },
        }));
      },
      
      // 更新LLM配置
      updateLLMConfiguration: (provider, config) => {
        set((state) => ({
          config: {
            ...state.config,
            llmConfiguration: {
              ...state.config.llmConfiguration,
              [provider]: {
                ...state.config.llmConfiguration[provider],
                ...config,
              },
            },
          },
        }));
      },
      
      // 更新语音模式
      updateVoiceMode: (mode) => {
        set((state) => ({
          config: {
            ...state.config,
            voiceMode: mode,
          },
        }));
      },
      
      // 更新LLM提供商
      updateLLMProvider: (provider) => {
        set((state) => ({
          config: {
            ...state.config,
            llmProvider: provider,
          },
        }));
      },
    }),
    {
      name: 'audio-config-storage',
    }
  )
);

// 选择器函数
export const selectConfig = (state: AudioConfigState) => state.config;
export const selectVoices = (state: AudioConfigState) => state.filteredVoices;
export const selectIsGenerating = (state: AudioConfigState) => state.isGenerating;
export const selectError = (state: AudioConfigState) => state.error;