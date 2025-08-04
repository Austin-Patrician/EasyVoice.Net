// App.tsx
import React, { useState, useRef } from "react";
import { Layout, Row, Col, Card, Typography, Space, message } from "antd";
import { useAppStore } from "./stores/useAppStore";
import TextInputArea from "./components/TextInputArea";
import VoiceSettingsPanel from "./components/VoiceSettingsPanel";
import ActionButtons from "./components/ActionButtons";
import GenerationProgress from "./components/GenerationProgress";
import AudioPlayer from "./components/AudioPlayer";
import AudioHistoryList from "./components/AudioHistoryList";
import AppFooter from "./components/AppFooter";
import "./App.css";

const { Content } = Layout;
const { Title, Paragraph } = Typography;

interface AudioItem {
  id: string;
  name: string;
  url: string;
  duration: number;
  createdAt: Date;
}

const App = () => {
  const {
    config,
    inputText,
    isGenerating,
    updateTtsConfig,
    updateLlmConfig,
    setInputText,
    setIsGenerating,
  } = useAppStore();

  // 音频播放状态
  const [currentAudio, setCurrentAudio] = useState<string | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(1);
  const [playbackRate, setPlaybackRate] = useState(1);
  const audioRef = useRef<HTMLAudioElement>(null);

  // 试听音频状态
  const [previewAudio, setPreviewAudio] = useState<string | null>(null);
  const [isPreviewPlaying, setIsPreviewPlaying] = useState(false);
  const previewAudioRef = useRef<HTMLAudioElement>(null);

  // 音频列表
  const [audioList, setAudioList] = useState<AudioItem[]>([]);

  // 文件上传处理
  const handleFileUpload = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const text = e.target?.result as string;
      setInputText(text);
      message.success("文件上传成功");
    };
    reader.readAsText(file);
    return false;
  };

  // 生成语音
  const handleGenerate = async () => {
    if (!inputText.trim()) {
      message.error("请输入要转换的文本");
      return;
    }
    setIsGenerating(true);
    try {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      const newAudio: AudioItem = {
        id: Date.now().toString(),
        name: `语音_${Date.now()}`,
        url: "data:audio/mp3;base64,",
        duration: 30,
        createdAt: new Date(),
      };
      setCurrentAudio(newAudio.url);
      setAudioList((prev) => [newAudio, ...prev]);
      message.success("语音生成成功");
    } catch (error) {
      message.error("生成失败，请重试");
    } finally {
      setIsGenerating(false);
    }
  };

  // 重置配置
  const handleReset = () => {
    setInputText("");
    setCurrentAudio(null);
    setIsPlaying(false);
    setAudioList([]);
    message.success("配置已重置");
  };

  // 试听功能
  const handlePreview = async () => {
    if (!config.llmConfig?.previewText?.trim()) {
      message.error("请输入试听文本");
      return;
    }
    try {
      await new Promise((resolve) => setTimeout(resolve, 1000));
      setPreviewAudio("data:audio/mp3;base64,");
      message.success("试听音频生成成功");
    } catch (error) {
      message.error("试听失败，请重试");
    }
  };

  const togglePreviewPlay = () => {
    if (previewAudioRef.current && previewAudio) {
      if (isPreviewPlaying) {
        previewAudioRef.current.pause();
      } else {
        previewAudioRef.current.play();
      }
      setIsPreviewPlaying(!isPreviewPlaying);
    }
  };

  // 音频播放控制
  const togglePlay = () => {
    if (audioRef.current && currentAudio) {
      if (isPlaying) {
        audioRef.current.pause();
      } else {
        audioRef.current.play();
      }
      setIsPlaying(!isPlaying);
    }
  };

  const fastForward = () => {
    if (audioRef.current) {
      audioRef.current.currentTime = Math.min(audioRef.current.currentTime + 10, duration);
    }
  };

  const fastBackward = () => {
    if (audioRef.current) {
      audioRef.current.currentTime = Math.max(audioRef.current.currentTime - 10, 0);
    }
  };

  const handleVolumeChange = (value: number) => {
    setVolume(value);
    if (audioRef.current) {
      audioRef.current.volume = value;
    }
  };

  const handlePlaybackRateChange = (rate: number) => {
    setPlaybackRate(rate);
    if (audioRef.current) {
      audioRef.current.playbackRate = rate;
    }
  };

  return (
    <Layout style={{ minHeight: "100vh", background: "#f0f2f5" }}>
      <Content style={{ maxWidth: 1800, margin: "0 auto", padding: "48px 24px 24px 24px" }}>
        <div style={{ marginBottom: 32, textAlign: "center" }}>
          <Title
            level={1}
            style={{
              background: "linear-gradient(90deg, #1677ff, #722ed1)",
              WebkitBackgroundClip: "text",
              color: "transparent",
              fontWeight: 700,
              marginBottom: 12,
              fontSize: 40,
              maxWidth: 600,
              marginLeft: "auto",
              marginRight: "auto",
              lineHeight: 1.2,
            }}
          >
            智能文本转语音
          </Title>
          <Paragraph type="secondary" style={{ fontSize: 20, margin: 0, maxWidth: 600, marginLeft: "auto", marginRight: "auto" }}>
            基于AI技术，将您的文本转换为自然流畅的高质量语音
          </Paragraph>
        </div>
        <Row gutter={[32, 32]} style={{ marginBottom: 32 }} wrap={false}>
          <Col xs={24} xl={14}>
            <Card style={{ minHeight: 600 }}>
              <TextInputArea
                inputText={inputText}
                setInputText={setInputText}
                handleFileUpload={handleFileUpload}
              />
            </Card>
          </Col>
          <Col xs={24} xl={12}>
            <Card style={{ minWidth: 400 }}>
              <VoiceSettingsPanel
                config={config}
                updateTtsConfig={updateTtsConfig}
                updateLlmConfig={updateLlmConfig}
                handlePreview={handlePreview}
                previewAudio={previewAudio}
                isPreviewPlaying={isPreviewPlaying}
                togglePreviewPlay={togglePreviewPlay}
                previewAudioRef={previewAudioRef}
              />
            </Card>
          </Col>
        </Row>
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <ActionButtons
            isGenerating={isGenerating}
            inputText={inputText}
            handleGenerate={handleGenerate}
            handleReset={handleReset}
          />
          <GenerationProgress isGenerating={isGenerating} />
          <AudioPlayer
            currentAudio={currentAudio}
            isPlaying={isPlaying}
            currentTime={currentTime}
            duration={duration}
            volume={volume}
            playbackRate={playbackRate}
            audioRef={audioRef}
            togglePlay={togglePlay}
            fastForward={fastForward}
            fastBackward={fastBackward}
            setCurrentTime={setCurrentTime}
            setDuration={setDuration}
            onVolumeChange={handleVolumeChange}
            onPlaybackRateChange={handlePlaybackRateChange}
            onEnded={() => setIsPlaying(false)}
          />
          <AudioHistoryList
            audioList={audioList}
            setCurrentAudio={setCurrentAudio}
            setIsPlaying={setIsPlaying}
          />
        </Space>
      </Content>
      <AppFooter />
    </Layout>
  );
};

export default App;
