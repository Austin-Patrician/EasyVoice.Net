import React, { useRef, useEffect, useState } from 'react';
import { Card, Progress, Typography } from 'antd';
import { AudioVisualizationData } from '../../types';
import { REALTIME_CONFIG } from '../../constants';
import './AudioVisualization.css';

const { Text } = Typography;

interface AudioVisualizationProps {
  visualizationData: AudioVisualizationData;
  isRecording?: boolean;
  isPlaying?: boolean;
  className?: string;
  width?: number;
  height?: number;
  showVolumeBar?: boolean;
  showFrequencyBars?: boolean;
  theme?: 'light' | 'dark';
}

const AudioVisualization: React.FC<AudioVisualizationProps> = ({
  visualizationData,
  isRecording = false,
  isPlaying = false,
  className = '',
  width = 300,
  height = 100,
  showVolumeBar = true,
  showFrequencyBars = true,
  theme = 'light'
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const animationFrameRef = useRef<number | null>(null);
  const [volumeLevel, setVolumeLevel] = useState(0);
  const [peakVolume, setPeakVolume] = useState(0);

  useEffect(() => {
    if ((visualizationData.isRecording || visualizationData.isPlaying) && showFrequencyBars) {
      drawVisualization();
    }
  }, [visualizationData, isRecording, isPlaying, theme, showFrequencyBars]);

  useEffect(() => {
    // 更新音量级别
    const currentVolume = Math.round(visualizationData.volume);
    setVolumeLevel(currentVolume);
    
    // 更新峰值音量
    if (currentVolume > peakVolume) {
      setPeakVolume(currentVolume);
      // 峰值音量缓慢衰减
      setTimeout(() => {
        setPeakVolume(prev => Math.max(prev - 1, 0));
      }, 100);
    }
  }, [visualizationData.volume, peakVolume]);

  const drawVisualization = () => {
    const canvas = canvasRef.current;
    if (!canvas || !visualizationData.frequencies.length) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const canvasWidth = canvas.width;
    const canvasHeight = canvas.height;
    const barCount = Math.min(visualizationData.frequencies.length, 64); // 限制条数以提高性能
    const barWidth = canvasWidth / barCount;
    const barSpacing = barWidth * 0.1;
    const actualBarWidth = barWidth - barSpacing;

    // 清除画布
    ctx.clearRect(0, 0, canvasWidth, canvasHeight);

    // 设置渐变背景
    const gradient = ctx.createLinearGradient(0, 0, 0, canvasHeight);
    if (theme === 'dark') {
      gradient.addColorStop(0, 'rgba(30, 41, 59, 0.1)');
      gradient.addColorStop(1, 'rgba(15, 23, 42, 0.1)');
    } else {
      gradient.addColorStop(0, 'rgba(248, 250, 252, 0.8)');
      gradient.addColorStop(1, 'rgba(226, 232, 240, 0.8)');
    }
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, canvasWidth, canvasHeight);

    // 绘制频谱条
    for (let i = 0; i < barCount; i++) {
      const dataIndex = Math.floor((i / barCount) * visualizationData.frequencies.length);
      const barHeight = (visualizationData.frequencies[dataIndex] / 255) * canvasHeight * 0.8;
      
      // 计算颜色
      let barColor;
      if (isRecording) {
        // 录音时使用红色系
        const intensity = visualizationData.frequencies[dataIndex] / 255;
        barColor = `rgba(239, 68, 68, ${0.3 + intensity * 0.7})`;
      } else if (isPlaying) {
        // 播放时使用绿色系
        const intensity = visualizationData.frequencies[dataIndex] / 255;
        barColor = `rgba(16, 185, 129, ${0.3 + intensity * 0.7})`;
      } else {
        // 默认使用蓝色系
        const hue = (i / barCount) * 60 + 200; // 蓝色到青色
        const intensity = visualizationData.frequencies[dataIndex] / 255;
        barColor = `hsla(${hue}, 70%, 60%, ${0.3 + intensity * 0.7})`;
      }

      // 绘制主条
      ctx.fillStyle = barColor;
      const x = i * barWidth + barSpacing / 2;
      const y = canvasHeight - barHeight;
      
      // 添加圆角效果
      const radius = actualBarWidth / 4;
      ctx.beginPath();
      ctx.roundRect(x, y, actualBarWidth, barHeight, [radius, radius, 0, 0]);
      ctx.fill();

      // 添加高光效果
      if (barHeight > canvasHeight * 0.3) {
        const highlightGradient = ctx.createLinearGradient(x, y, x, y + barHeight);
        highlightGradient.addColorStop(0, 'rgba(255, 255, 255, 0.3)');
        highlightGradient.addColorStop(0.3, 'rgba(255, 255, 255, 0.1)');
        highlightGradient.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = highlightGradient;
        ctx.beginPath();
        ctx.roundRect(x, y, actualBarWidth, barHeight, [radius, radius, 0, 0]);
        ctx.fill();
      }
    }

    // 绘制中心线（静音时）
    if (!isRecording && !isPlaying && visualizationData.volume < 5) {
      ctx.strokeStyle = theme === 'dark' ? 'rgba(148, 163, 184, 0.5)' : 'rgba(100, 116, 139, 0.5)';
      ctx.lineWidth = 1;
      ctx.setLineDash([5, 5]);
      ctx.beginPath();
      ctx.moveTo(0, canvasHeight / 2);
      ctx.lineTo(canvasWidth, canvasHeight / 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  };

  const getVolumeColor = () => {
    if (volumeLevel < 30) return '#52c41a'; // 绿色
    if (volumeLevel < 70) return '#faad14'; // 黄色
    return '#ff4d4f'; // 红色
  };

  const getStatusText = () => {
    if (isRecording) return '正在录音...';
    if (isPlaying) return '正在播放...';
    if (visualizationData.isRecording || visualizationData.isPlaying) return '监听中...';
    return '静音';
  };

  const getStatusColor = () => {
    if (isRecording) return '#ff4d4f';
    if (isPlaying) return '#52c41a';
    if (visualizationData.isRecording || visualizationData.isPlaying) return '#1890ff';
    return '#8c8c8c';
  };

  return (
    <div className={`audio-visualization ${theme} ${className}`}>
      {/* 状态指示器 */}
      <div className="status-indicator">
        <div 
          className={`status-dot ${isRecording ? 'recording' : isPlaying ? 'playing' : 'idle'}`}
          style={{ backgroundColor: getStatusColor() }}
        />
        <Text className="status-text" style={{ color: getStatusColor() }}>
          {getStatusText()}
        </Text>
      </div>

      {/* 频谱可视化 */}
      {showFrequencyBars && (
        <div className="frequency-visualization">
          <canvas
            ref={canvasRef}
            width={width}
            height={height}
            className="visualization-canvas"
            style={{ width: `${width}px`, height: `${height}px` }}
          />
        </div>
      )}

      {/* 音量条 */}
      {showVolumeBar && (
        <div className="volume-section">
          <div className="volume-info">
            <Text className="volume-label">音量</Text>
            <Text className="volume-value">{volumeLevel}%</Text>
          </div>
          <div className="volume-bars">
            {/* 当前音量 */}
            <Progress
              percent={volumeLevel}
              showInfo={false}
              strokeColor={getVolumeColor()}
              trailColor={theme === 'dark' ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}
              className="volume-progress"
            />
            {/* 峰值指示器 */}
            {peakVolume > 0 && (
              <div 
                className="peak-indicator"
                style={{ 
                  left: `${peakVolume}%`,
                  backgroundColor: getVolumeColor()
                }}
              />
            )}
          </div>
        </div>
      )}

      {/* 音频统计信息 */}
      <div className="audio-stats">
        <div className="stat-item">
          <Text className="stat-label">采样率</Text>
          <Text className="stat-value">{REALTIME_CONFIG.INPUT_SAMPLE_RATE}Hz</Text>
        </div>
        <div className="stat-item">
          <Text className="stat-label">声道</Text>
          <Text className="stat-value">单声道</Text>
        </div>
        <div className="stat-item">
          <Text className="stat-label">格式</Text>
          <Text className="stat-value">PCM</Text>
        </div>
      </div>
    </div>
  );
};

export default AudioVisualization;