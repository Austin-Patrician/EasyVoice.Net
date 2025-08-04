// AudioPlayer.tsx
import React, { useEffect } from "react";
import { Card, Typography, Button, Slider, Space, Popover, Tooltip } from "antd";
import {
  FastBackwardOutlined,
  FastForwardOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  SoundOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons";

interface AudioPlayerProps {
  currentAudio: string | null;
  isPlaying: boolean;
  currentTime: number;
  duration: number;
  volume: number;
  playbackRate: number;
  audioRef: React.RefObject<HTMLAudioElement | null>;
  togglePlay: () => void;
  fastForward: () => void;
  fastBackward: () => void;
  setCurrentTime: (time: number) => void;
  setDuration: (duration: number) => void;
  onVolumeChange: (volume: number) => void;
  onPlaybackRateChange: (rate: number) => void;
  onEnded: () => void;
}

const playbackRates = [
  { value: 0.5, label: "0.5x" },
  { value: 0.75, label: "0.75x" },
  { value: 1, label: "1x" },
  { value: 1.25, label: "1.25x" },
  { value: 1.5, label: "1.5x" },
  { value: 2, label: "2x" },
];

const formatTime = (seconds: number) => {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, "0")}`;
};

const AudioPlayer: React.FC<AudioPlayerProps> = ({
  currentAudio,
  isPlaying,
  currentTime,
  duration,
  volume,
  playbackRate,
  audioRef,
  togglePlay,
  fastForward,
  fastBackward,
  setCurrentTime,
  setDuration,
  onVolumeChange,
  onPlaybackRateChange,
  onEnded,
}) => {
  useEffect(() => {
    if (audioRef.current) {
      audioRef.current.volume = volume;
      audioRef.current.playbackRate = playbackRate;
    }
  }, [volume, playbackRate]);

  if (!currentAudio) return null;

  const volumeContent = (
    <div style={{ width: 100, padding: "8px 0" }}>
      <Slider
        vertical
        min={0}
        max={1}
        step={0.01}
        value={volume}
        onChange={onVolumeChange}
        style={{ height: 100 }}
      />
      <div style={{ textAlign: "center", marginTop: 8 }}>
        <Typography.Text style={{ fontSize: 12 }}>
          {Math.round(volume * 100)}%
        </Typography.Text>
      </div>
    </div>
  );

  const speedContent = (
    <div style={{ padding: "8px 0" }}>
      <Space direction="vertical" size="small">
        {playbackRates.map((rate) => (
          <Button
            key={rate.value}
            type={playbackRate === rate.value ? "primary" : "text"}
            size="small"
            onClick={() => onPlaybackRateChange(rate.value)}
            style={{ width: 60, textAlign: "center" }}
          >
            {rate.label}
          </Button>
        ))}
      </Space>
    </div>
  );

  return (
    <Card
      style={{
        background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
        borderRadius: 16,
        border: "none",
        boxShadow: "0 8px 32px rgba(0,0,0,0.1)",
      }}
      bodyStyle={{ padding: 32 }}
    >
      {/* 标题区域 */}
      <div style={{ textAlign: "center", marginBottom: 32 }}>
        <Typography.Title
          level={3}
          style={{
            color: "white",
            margin: 0,
            fontWeight: 600,
            textShadow: "0 2px 4px rgba(0,0,0,0.2)",
          }}
        >
          🎧 音频播放器
        </Typography.Title>
        <Typography.Text
          style={{
            color: "rgba(255,255,255,0.8)",
            fontSize: 16,
            display: "block",
            marginTop: 8,
          }}
        >
          高品质语音播放体验
        </Typography.Text>
      </div>

      {/* 进度条区域 */}
      <div
        style={{
          background: "rgba(255,255,255,0.1)",
          borderRadius: 12,
          padding: 24,
          marginBottom: 24,
          backdropFilter: "blur(10px)",
        }}
      >
        <Slider
          min={0}
          max={duration || 1}
          value={currentTime}
          onChange={(value) => {
            if (audioRef.current) audioRef.current.currentTime = Number(value);
            setCurrentTime(Number(value));
          }}
          style={{ margin: "0 8px" }}
          trackStyle={{ background: "linear-gradient(90deg, #ff6b6b, #4ecdc4)" }}
          handleStyle={{
            borderColor: "#fff",
            backgroundColor: "#fff",
            boxShadow: "0 2px 8px rgba(0,0,0,0.2)",
          }}
        />
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginTop: 12,
            color: "rgba(255,255,255,0.9)",
            fontFamily: "monospace",
            fontSize: 14,
          }}
        >
          <span>{formatTime(currentTime)}</span>
          <span>{formatTime(duration)}</span>
        </div>
      </div>

      {/* 控制按钮区域 */}
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          gap: 16,
          marginBottom: 24,
        }}
      >
        <Tooltip title="快退 10 秒">
          <Button
            icon={<FastBackwardOutlined />}
            onClick={fastBackward}
            size="large"
            shape="circle"
            style={{
              width: 48,
              height: 48,
              background: "rgba(255,255,255,0.15)",
              border: "1px solid rgba(255,255,255,0.2)",
              color: "white",
              backdropFilter: "blur(10px)",
            }}
          />
        </Tooltip>

        <Button
          type="primary"
          icon={isPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
          onClick={togglePlay}
          shape="circle"
          size="large"
          style={{
            width: 64,
            height: 64,
            background: "linear-gradient(45deg, #ff6b6b, #4ecdc4)",
            border: "none",
            fontSize: 24,
            boxShadow: "0 4px 16px rgba(0,0,0,0.2)",
            transform: isPlaying ? "scale(0.95)" : "scale(1)",
            transition: "all 0.2s ease",
          }}
        />

        <Tooltip title="快进 10 秒">
          <Button
            icon={<FastForwardOutlined />}
            onClick={fastForward}
            size="large"
            shape="circle"
            style={{
              width: 48,
              height: 48,
              background: "rgba(255,255,255,0.15)",
              border: "1px solid rgba(255,255,255,0.2)",
              color: "white",
              backdropFilter: "blur(10px)",
            }}
          />
        </Tooltip>
      </div>

      {/* 音量和倍速控制 */}
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          gap: 24,
        }}
      >
        <Popover
          content={volumeContent}
          title="音量控制"
          trigger="click"
          placement="top"
        >
          <Button
            icon={<SoundOutlined />}
            style={{
              background: "rgba(255,255,255,0.15)",
              border: "1px solid rgba(255,255,255,0.2)",
              color: "white",
              backdropFilter: "blur(10px)",
            }}
          >
            音量 {Math.round(volume * 100)}%
          </Button>
        </Popover>

        <Popover
          content={speedContent}
          title="播放速度"
          trigger="click"
          placement="top"
        >
          <Button
            icon={<ThunderboltOutlined />}
            style={{
              background: "rgba(255,255,255,0.15)",
              border: "1px solid rgba(255,255,255,0.2)",
              color: "white",
              backdropFilter: "blur(10px)",
            }}
          >
            倍速 {playbackRate}x
          </Button>
        </Popover>
      </div>

      <audio
        ref={audioRef}
        src={currentAudio}
        onTimeUpdate={(e) => setCurrentTime(e.currentTarget.currentTime)}
        onLoadedMetadata={(e) => setDuration(e.currentTarget.duration)}
        onEnded={onEnded}
      />
    </Card>
  );
};

export default AudioPlayer;
