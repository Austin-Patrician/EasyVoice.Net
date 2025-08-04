import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Button, Slider, Tooltip, Dropdown, Space } from 'antd';
import {
  Play,
  Pause,
  Volume2,
  VolumeX,
  SkipBack,
  SkipForward,
  RotateCcw,
  Settings,
  Download
} from 'lucide-react';
import { cn } from '../../lib/utils';

interface AudioPlayerProps {
  src?: string;
  title?: string;
  className?: string;
  onDownload?: () => void;
  onEnded?: () => void;
  autoPlay?: boolean;
  showDownload?: boolean;
  compact?: boolean;
}

interface PlaybackState {
  isPlaying: boolean;
  currentTime: number;
  duration: number;
  volume: number;
  playbackRate: number;
  isMuted: boolean;
  isLoading: boolean;
}

const PLAYBACK_RATES = [0.5, 0.75, 1, 1.25, 1.5, 2];
const SKIP_TIME = 10; // 快进快退秒数

export const AudioPlayer: React.FC<AudioPlayerProps> = ({
  src,
  title = '音频播放',
  className = '',
  onDownload,
  onEnded,
  autoPlay = false,
  showDownload = true,
  compact = false
}) => {
  const audioRef = useRef<HTMLAudioElement>(null);
  const progressRef = useRef<HTMLDivElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  
  const [state, setState] = useState<PlaybackState>({
    isPlaying: false,
    currentTime: 0,
    duration: 0,
    volume: 0.8,
    playbackRate: 1,
    isMuted: false,
    isLoading: false
  });

  // 格式化时间显示
  const formatTime = useCallback((time: number): string => {
    if (!isFinite(time)) return '0:00';
    const minutes = Math.floor(time / 60);
    const seconds = Math.floor(time % 60);
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }, []);

  // 音频事件处理
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    const handleLoadStart = () => setState(prev => ({ ...prev, isLoading: true }));
    const handleLoadedData = () => {
      setState(prev => ({ 
        ...prev, 
        duration: audio.duration || 0,
        isLoading: false 
      }));
    };
    
    const handleTimeUpdate = () => {
      if (!isDragging) {
        setState(prev => ({ ...prev, currentTime: audio.currentTime }));
      }
    };
    
    const handleEnded = () => {
      setState(prev => ({ ...prev, isPlaying: false, currentTime: 0 }));
      onEnded?.();
    };
    
    const handleError = () => {
      setState(prev => ({ ...prev, isLoading: false, isPlaying: false }));
    };

    audio.addEventListener('loadstart', handleLoadStart);
    audio.addEventListener('loadeddata', handleLoadedData);
    audio.addEventListener('timeupdate', handleTimeUpdate);
    audio.addEventListener('ended', handleEnded);
    audio.addEventListener('error', handleError);

    return () => {
      audio.removeEventListener('loadstart', handleLoadStart);
      audio.removeEventListener('loadeddata', handleLoadedData);
      audio.removeEventListener('timeupdate', handleTimeUpdate);
      audio.removeEventListener('ended', handleEnded);
      audio.removeEventListener('error', handleError);
    };
  }, [isDragging]);

  // 设置音频源
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || !src) return;

    audio.src = src;
    audio.volume = state.volume;
    audio.playbackRate = state.playbackRate;
    
    if (autoPlay) {
      audio.play().catch(console.error);
    }
  }, [src, autoPlay]);

  // 更新音频属性
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    audio.volume = state.isMuted ? 0 : state.volume;
    audio.playbackRate = state.playbackRate;
  }, [state.volume, state.playbackRate, state.isMuted]);

  // 播放/暂停控制
  const togglePlay = useCallback(async () => {
    const audio = audioRef.current;
    if (!audio || !src) return;

    try {
      if (state.isPlaying) {
        audio.pause();
        setState(prev => ({ ...prev, isPlaying: false }));
      } else {
        await audio.play();
        setState(prev => ({ ...prev, isPlaying: true }));
      }
    } catch (error) {
      console.error('播放失败:', error);
    }
  }, [state.isPlaying, src]);

  // 进度条拖拽
  const handleProgressClick = useCallback((e: React.MouseEvent) => {
    const audio = audioRef.current;
    const progressBar = progressRef.current;
    if (!audio || !progressBar || !state.duration) return;

    const rect = progressBar.getBoundingClientRect();
    const clickX = e.clientX - rect.left;
    const newTime = (clickX / rect.width) * state.duration;
    
    audio.currentTime = newTime;
    setState(prev => ({ ...prev, currentTime: newTime }));
  }, [state.duration]);

  // 快进快退
  const skip = useCallback((seconds: number) => {
    const audio = audioRef.current;
    if (!audio) return;

    const newTime = Math.max(0, Math.min(state.duration, audio.currentTime + seconds));
    audio.currentTime = newTime;
    setState(prev => ({ ...prev, currentTime: newTime }));
  }, [state.duration]);

  // 音量控制
  const handleVolumeChange = useCallback((value: number) => {
    setState(prev => ({ ...prev, volume: value, isMuted: false }));
  }, []);

  // 静音切换
  const toggleMute = useCallback(() => {
    setState(prev => ({ ...prev, isMuted: !prev.isMuted }));
  }, []);

  // 播放速度控制
  const handleRateChange = useCallback((rate: number) => {
    setState(prev => ({ ...prev, playbackRate: rate }));
  }, []);

  // 重置播放
  const resetPlayback = useCallback(() => {
    const audio = audioRef.current;
    if (!audio) return;

    audio.currentTime = 0;
    setState(prev => ({ ...prev, currentTime: 0, isPlaying: false }));
  }, []);

  // 进度百分比
  const progressPercent = state.duration > 0 ? (state.currentTime / state.duration) * 100 : 0;

  // 播放速度菜单
  const speedMenuItems = PLAYBACK_RATES.map(rate => ({
    key: rate.toString(),
    label: (
      <div 
        className={cn(
          'px-2 py-1 cursor-pointer hover:bg-gray-100 rounded',
          state.playbackRate === rate && 'bg-blue-50 text-blue-600'
        )}
        onClick={() => handleRateChange(rate)}
      >
        {rate}x
      </div>
    )
  }));

  if (compact) {
    return (
      <div className={cn('flex items-center space-x-2 p-2 bg-gray-50 rounded-lg', className)}>
        <audio ref={audioRef} />
        
        <Button
          type="text"
          icon={state.isPlaying ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4" />}
          onClick={togglePlay}
          disabled={!src || state.isLoading}
          loading={state.isLoading}
          size="small"
        />
        
        <div className="flex-1 min-w-0">
          <div className="text-xs text-gray-600 truncate">{title}</div>
          <div className="text-xs text-gray-400">
            {formatTime(state.currentTime)} / {formatTime(state.duration)}
          </div>
        </div>
        
        {showDownload && onDownload && (
          <Button
            type="text"
            icon={<Download className="w-4 h-4" />}
            onClick={onDownload}
            size="small"
          />
        )}
      </div>
    );
  }

  return (
    <div className={cn('bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden', className)}>
      <audio ref={audioRef} />
      
      {/* 标题栏 */}
      <div className="px-6 py-4 bg-gradient-to-r from-blue-50 to-purple-50 border-b border-gray-100">
        <div className="flex items-center justify-between">
          <div className="flex-1 min-w-0">
            <h3 className="text-lg font-semibold text-gray-900 truncate">{title}</h3>
            <div className="flex items-center space-x-4 mt-1 text-sm text-gray-500">
              <span>{formatTime(state.currentTime)} / {formatTime(state.duration)}</span>
              <span>速度: {state.playbackRate}x</span>
            </div>
          </div>
          
          {showDownload && onDownload && (
            <Button
              type="primary"
              icon={<Download className="w-4 h-4" />}
              onClick={onDownload}
              className="ml-4"
            >
              下载
            </Button>
          )}
        </div>
      </div>

      {/* 进度条 */}
      <div className="px-6 py-4">
        <div 
          ref={progressRef}
          className="relative h-2 bg-gray-200 rounded-full cursor-pointer group"
          onClick={handleProgressClick}
        >
          <div 
            className="absolute top-0 left-0 h-full bg-gradient-to-r from-blue-500 to-purple-500 rounded-full transition-all duration-150"
            style={{ width: `${progressPercent}%` }}
          />
          <div 
            className="absolute top-1/2 transform -translate-y-1/2 w-4 h-4 bg-white border-2 border-blue-500 rounded-full shadow-md opacity-0 group-hover:opacity-100 transition-opacity duration-150"
            style={{ left: `calc(${progressPercent}% - 8px)` }}
          />
        </div>
      </div>

      {/* 控制按钮 */}
      <div className="px-6 py-4 bg-gray-50">
        <div className="flex items-center justify-between">
          {/* 主要控制 */}
          <div className="flex items-center space-x-3">
            <Button
              type="text"
              icon={<SkipBack className="w-5 h-5" />}
              onClick={() => skip(-SKIP_TIME)}
              disabled={!src}
              className="hover:bg-gray-200"
            />
            
            <Button
              type="primary"
              size="large"
              icon={state.isPlaying ? <Pause className="w-6 h-6" /> : <Play className="w-6 h-6" />}
              onClick={togglePlay}
              disabled={!src || state.isLoading}
              loading={state.isLoading}
              className="w-12 h-12 rounded-full flex items-center justify-center"
            />
            
            <Button
              type="text"
              icon={<SkipForward className="w-5 h-5" />}
              onClick={() => skip(SKIP_TIME)}
              disabled={!src}
              className="hover:bg-gray-200"
            />
            
            <Button
              type="text"
              icon={<RotateCcw className="w-5 h-5" />}
              onClick={resetPlayback}
              disabled={!src}
              className="hover:bg-gray-200"
            />
          </div>

          {/* 音量和设置 */}
          <div className="flex items-center space-x-4">
            {/* 音量控制 */}
            <div className="flex items-center space-x-2">
              <Button
                type="text"
                icon={state.isMuted || state.volume === 0 ? 
                  <VolumeX className="w-5 h-5" /> : 
                  <Volume2 className="w-5 h-5" />
                }
                onClick={toggleMute}
                className="hover:bg-gray-200"
              />
              <div className="w-20">
                <Slider
                  min={0}
                  max={1}
                  step={0.1}
                  value={state.isMuted ? 0 : state.volume}
                  onChange={handleVolumeChange}
                  tooltip={{ formatter: (value) => `${Math.round((value || 0) * 100)}%` }}
                />
              </div>
            </div>

            {/* 播放速度 */}
            <Dropdown
              menu={{ items: speedMenuItems }}
              trigger={['click']}
              placement="topRight"
            >
              <Button
                type="text"
                icon={<Settings className="w-5 h-5" />}
                className="hover:bg-gray-200"
              >
                {state.playbackRate}x
              </Button>
            </Dropdown>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AudioPlayer;