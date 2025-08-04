import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { GenerationState, AudioItem } from '../types';

interface DownloadState {
  isDownloading: boolean;
  progress: number;
}

interface GenerationStoreState extends GenerationState {
  // 下载状态
  downloadState: DownloadState;
  
  // Actions
  setAudio: (audio: string) => void;
  setFile: (file: string) => void;
  setProgress: (progress: number) => void;
  addAudioItem: (item: Omit<AudioItem, 'isDownloading' | 'isSrtLoading' | 'isPlaying' | 'progress'>) => void;
  updateAudioItem: (index: number, updates: Partial<AudioItem>) => void;
  removeAudioItem: (index: number) => void;
  clearAudioList: () => void;
  
  // 下载相关
  setDownloadState: (state: Partial<DownloadState>) => void;
  startDownload: (index: number) => void;
  finishDownload: (index: number) => void;
  updateDownloadProgress: (index: number, progress: number) => void;
  
  // 播放相关
  setPlaying: (index: number, playing: boolean) => void;
  stopAllPlaying: () => void;
  
  // SRT相关
  setSrtLoading: (index: number, loading: boolean) => void;
  
  // 重置状态
  reset: () => void;
}

const initialState: GenerationState = {
  audio: null,
  file: null,
  progress: 0,
  audioList: [],
};

const initialDownloadState: DownloadState = {
  isDownloading: false,
  progress: 0,
};

export const useGenerationStore = create<GenerationStoreState>()(
  persist(
    (set, get) => ({
      ...initialState,
      downloadState: initialDownloadState,
      
      // 设置当前音频
      setAudio: (audio) => {
        set({ audio });
      },
      
      // 设置当前文件
      setFile: (file) => {
        set({ file });
      },
      
      // 设置进度
      setProgress: (progress) => {
        set({ progress });
      },
      
      // 添加音频项目
      addAudioItem: (item) => {
        const newItem: AudioItem = {
          ...item,
          isDownloading: false,
          isSrtLoading: false,
          isPlaying: false,
          progress: 0,
        };
        
        set((state) => ({
          audioList: [newItem, ...state.audioList],
        }));
      },
      
      // 更新音频项目
      updateAudioItem: (index, updates) => {
        set((state) => {
          const newAudioList = [...state.audioList];
          if (newAudioList[index]) {
            newAudioList[index] = { ...newAudioList[index], ...updates };
          }
          return { audioList: newAudioList };
        });
      },
      
      // 删除音频项目
      removeAudioItem: (index) => {
        set((state) => ({
          audioList: state.audioList.filter((_, i) => i !== index),
        }));
      },
      
      // 清空音频列表
      clearAudioList: () => {
        set({ audioList: [] });
      },
      
      // 设置下载状态
      setDownloadState: (state) => {
        set((prevState) => ({
          downloadState: { ...prevState.downloadState, ...state }
        }));
      },
      
      // 开始下载
      startDownload: (index) => {
        get().updateAudioItem(index, { 
          isDownloading: true, 
          progress: 0 
        });
      },
      
      // 完成下载
      finishDownload: (index) => {
        get().updateAudioItem(index, { 
          isDownloading: false, 
          progress: 100 
        });
      },
      
      // 更新下载进度
      updateDownloadProgress: (index, progress) => {
        get().updateAudioItem(index, { progress });
      },
      
      // 设置播放状态
      setPlaying: (index, playing) => {
        set((state) => {
          const newAudioList = state.audioList.map((item, i) => ({
            ...item,
            isPlaying: i === index ? playing : false, // 只允许一个音频播放
          }));
          return { audioList: newAudioList };
        });
      },
      
      // 停止所有播放
      stopAllPlaying: () => {
        set((state) => ({
          audioList: state.audioList.map(item => ({
            ...item,
            isPlaying: false,
          })),
        }));
      },
      
      // 设置SRT加载状态
      setSrtLoading: (index, loading) => {
        get().updateAudioItem(index, { isSrtLoading: loading });
      },
      
      // 重置状态
      reset: () => {
        set(initialState);
      },
    }),
    {
      name: 'easy-voice-app-store',
      // 只持久化音频列表，不持久化临时状态
      partialize: (state) => ({ 
        audioList: state.audioList.map(item => ({
          ...item,
          isDownloading: false,
          isSrtLoading: false,
          isPlaying: false,
        })),
      }),
    }
  )
);

// 选择器函数
export const selectAudioList = (state: GenerationStoreState) => state.audioList;
export const selectCurrentAudio = (state: GenerationStoreState) => state.audio;
export const selectProgress = (state: GenerationStoreState) => state.progress;
export const selectIsAnyDownloading = (state: GenerationStoreState) => 
  state.audioList.some(item => item.isDownloading);
export const selectIsAnyPlaying = (state: GenerationStoreState) => 
  state.audioList.some(item => item.isPlaying);