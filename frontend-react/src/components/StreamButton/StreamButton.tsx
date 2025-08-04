import React, { useState, useRef, useEffect } from 'react';
import { Button, message } from 'antd';
import { Play, Volume2 } from 'lucide-react';
import { apiService } from '../../services/api';
import { AudioPlayer } from '../AudioPlayer';
import type { AudioConfig } from '../../types';

interface StreamButtonProps {
  text: string;
  audioConfig: AudioConfig;
  disabled?: boolean;
  size?: 'small' | 'middle' | 'large';
  className?: string;
}

type StreamState = 'idle' | 'loading' | 'ready' | 'error';

export const StreamButton: React.FC<StreamButtonProps> = ({
  text,
  audioConfig,
  disabled = false,
  size = 'middle',
  className = ''
}) => {
  const [streamState, setStreamState] = useState<StreamState>('idle');
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [audioTitle, setAudioTitle] = useState<string>('');
  const abortControllerRef = useRef<AbortController | null>(null);

  // 清理资源
  useEffect(() => {
    return () => {
      if (audioUrl) {
        URL.revokeObjectURL(audioUrl);
      }
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [audioUrl]);

  const handleGenerate = async () => {
    if (!text.trim()) {
      message.warning('请输入要转换的文本');
      return;
    }

    try {
      setStreamState('loading');
      
      // 取消之前的请求
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      
      abortControllerRef.current = new AbortController();
      
      // 根据语音模式生成音频
      const response = await apiService.generateVoiceByMode(
        text.slice(0, 100), // 限制预览文本长度
        audioConfig.voiceMode,
        audioConfig.llmProvider,
        // Edge配置
        {
          voice: audioConfig.selectedVoice,
          rate: audioConfig.rate,
          pitch: audioConfig.pitch,
          volume: audioConfig.volume,
        },
        // LLM配置
        audioConfig.llmConfiguration
      );
      
      // 清理之前的音频URL
      if (audioUrl) {
        URL.revokeObjectURL(audioUrl);
      }
      
      // 设置新的音频URL和标题
      const newAudioUrl = response.audio;
      setAudioUrl(newAudioUrl);
      
      // 根据语音模式设置标题
      let title = '语音预览';
      if (audioConfig.voiceMode === 'edge') {
        title = `语音预览 - ${audioConfig.selectedVoice}`;
      } else {
        const providerName = audioConfig.llmProvider === 'openai' ? 'OpenAI' : '豆包';
        const voiceName = audioConfig.llmProvider === 'openai' 
          ? audioConfig.llmConfiguration.openai.voice 
          : '豆包语音';
        title = `语音预览 - ${providerName} (${voiceName})`;
      }
      
      setAudioTitle(title);
      setStreamState('ready');
      
    } catch (error: any) {
      if (error.name === 'AbortError') {
        setStreamState('idle');
        return;
      }
      
      console.error('Stream generate error:', error);
      setStreamState('error');
      message.error(error.message || '语音预览失败');
    }
  };

  const handleReset = () => {
    if (audioUrl) {
      URL.revokeObjectURL(audioUrl);
      setAudioUrl(null);
    }
    
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    
    setStreamState('idle');
    setAudioTitle('');
  };

  const getButtonIcon = () => {
    switch (streamState) {
      case 'loading':
        return <Volume2 className="w-4 h-4 animate-pulse" />;
      case 'ready':
        return <Play className="w-4 h-4" />;
      case 'error':
        return <Volume2 className="w-4 h-4" />;
      default:
        return <Volume2 className="w-4 h-4" />;
    }
  };

  const getButtonText = () => {
    switch (streamState) {
      case 'loading':
        return '生成中...';
      case 'ready':
        return '重新生成';
      case 'error':
        return '重试';
      default:
        return '生成预览';
    }
  };

  const isLoading = streamState === 'loading';
  const isDisabled = disabled || isLoading;

  return (
    <div className={`space-y-4 ${className}`}>
      {/* 生成按钮 */}
      <Button
        type={streamState === 'ready' ? 'default' : 'primary'}
        size={size}
        icon={getButtonIcon()}
        loading={isLoading}
        disabled={isDisabled}
        onClick={handleGenerate}
        block
        className="min-h-[40px]"
      >
        {getButtonText()}
      </Button>

      {/* 音频播放器 */}
      {streamState === 'ready' && audioUrl && (
        <div className="border border-gray-200 rounded-lg p-4 bg-gray-50">
          <AudioPlayer
            src={audioUrl}
            title={audioTitle}
            compact={true}
            onEnded={handleReset}
            className="w-full"
          />
        </div>
      )}
    </div>
  );
};