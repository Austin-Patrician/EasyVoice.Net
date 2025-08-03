import React, { useState, useRef } from 'react';
import { 
  Card, 
  Form, 
  Input, 
  Select, 
  Slider, 
  Button, 
  Space, 
  Row, 
  Col, 
  Typography, 
  Divider,
  Upload,
  message,
  Radio
} from 'antd';
import { 
  SoundOutlined, 
  PlayCircleOutlined, 
  DownloadOutlined, 
  UploadOutlined,
  PauseCircleOutlined,
  SettingOutlined,
  FileTextOutlined,
  LoadingOutlined
} from '@ant-design/icons';
import { useAppStore } from '../stores/useAppStore';
import { EasyVoiceApi } from '../services/api';
import type { TtsRequest } from '../services/api';

const { TextArea } = Input;
const { Option } = Select;
const { Title, Text } = Typography;

const TtsGenerator: React.FC = () => {
  const {
    config,
    inputText,
    isGenerating,
    generationResult,
    error,
    setInputText,
    setIsGenerating,
    setGenerationResult,
    setError,
    clearError,
    updateTtsConfig,
  } = useAppStore();

  const [isPlaying, setIsPlaying] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  const audioRef = useRef<HTMLAudioElement>(null);
  const [form] = Form.useForm();

  const handleGenerate = async () => {
    if (!inputText.trim()) {
      message.error('请输入要转换的文本');
      return;
    }

    clearError();
    setIsGenerating(true);

    try {
      const request: TtsRequest = {
        text: inputText,
        voice: config.ttsConfig.voice,
        speed: config.ttsConfig.speed,
        pitch: config.ttsConfig.pitch,
        volume: config.ttsConfig.volume,
        responseFormat: config.ttsConfig.responseFormat,
      };

      const response = config.useEdgeTtsAsDefault
        ? await EasyVoiceApi.generateTts(request)
        : await EasyVoiceApi.generateIntelligentTts({ ...request, useLlm: config.enableLlmAnalysis });

      if (response.success && response.data) {
        setGenerationResult({
          audioData: response.data.audioData || '',
          fileName: response.data.fileName || 'audio.mp3',
          engineUsed: response.data.engineUsed || 'Edge TTS',
        });
      } else {
        message.error(response.message || '生成失败');
      }
    } catch (error) {
      console.error('TTS generation error:', error);
      message.error('生成过程中发生错误，请稍后重试');
    } finally {
      setIsGenerating(false);
    }
  };

  const handlePlay = () => {
    if (audioRef.current && generationResult) {
      if (isPlaying) {
        audioRef.current.pause();
        setIsPlaying(false);
      } else {
        audioRef.current.play();
        setIsPlaying(true);
      }
    }
  };

  const handleDownload = () => {
    if (generationResult) {
      const link = document.createElement('a');
      link.href = `data:audio/${config.ttsConfig.responseFormat};base64,${generationResult.audioData}`;
      link.download = generationResult.fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  };

  const handleAudioEnded = () => {
    setIsPlaying(false);
  };

  return (
    <div className="space-y-6">
      <Card className="w-full">
        <div className="flex items-center justify-between mb-6">
          <Title level={2} className="mb-0">
            <SoundOutlined className="text-blue-600 mr-2" />
            语音合成
          </Title>
          <Button
            icon={<SettingOutlined />}
            onClick={() => setShowSettings(!showSettings)}
            type={showSettings ? 'primary' : 'default'}
          >
            {showSettings ? '隐藏设置' : '显示设置'}
          </Button>
        </div>

        {/* 文本输入区域 */}
        <Form form={form} layout="vertical">
          <Form.Item
            label={
              <span>
                <FileTextOutlined className="mr-1" />
                输入文本
              </span>
            }
          >
            <TextArea
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              placeholder="请输入要转换为语音的文本..."
              rows={4}
              disabled={isGenerating}
              showCount
              maxLength={5000}
            />
          </Form.Item>
        </Form>

        {/* 生成按钮 */}
        <Button
          type="primary"
          size="large"
          block
          onClick={handleGenerate}
          disabled={isGenerating || !inputText.trim()}
          icon={isGenerating ? <LoadingOutlined spin /> : <SoundOutlined />}
          loading={isGenerating}
        >
          {isGenerating ? '生成中...' : '生成语音'}
        </Button>
      </Card>

      {/* 语音设置面板 */}
      {showSettings && (
        <Card title="语音设置" size="small">
          <Row gutter={[16, 16]}>
            {/* 语音选择 */}
            <Col xs={24} md={12} lg={6}>
              <Form.Item label="语音">
                <Select
                  value={config.ttsConfig.voice}
                  onChange={(value) => updateTtsConfig({ voice: value })}
                  style={{ width: '100%' }}
                >
                  <Option value="zh-CN-YunxiNeural">云希 (男声)</Option>
                  <Option value="zh-CN-XiaoxiaoNeural">晓晓 (女声)</Option>
                  <Option value="zh-CN-YunyangNeural">云扬 (男声)</Option>
                  <Option value="zh-CN-XiaoyiNeural">晓伊 (女声)</Option>
                  <Option value="en-US-JennyNeural">Jenny (Female)</Option>
                  <Option value="en-US-GuyNeural">Guy (Male)</Option>
                </Select>
              </Form.Item>
            </Col>

            {/* 语速 */}
            <Col xs={24} md={12} lg={6}>
              <Form.Item label={`语速: ${config.ttsConfig.speed}x`}>
                <Slider
                  min={0.5}
                  max={2.0}
                  step={0.1}
                  value={config.ttsConfig.speed}
                  onChange={(value) => updateTtsConfig({ speed: value })}
                />
              </Form.Item>
            </Col>

            {/* 音调 */}
            <Col xs={24} md={12} lg={6}>
              <Form.Item label={`音调: ${config.ttsConfig.pitch > 0 ? '+' : ''}${config.ttsConfig.pitch}`}>
                <Slider
                  min={-50}
                  max={50}
                  step={5}
                  value={config.ttsConfig.pitch}
                  onChange={(value) => updateTtsConfig({ pitch: value })}
                />
              </Form.Item>
            </Col>

            {/* 音量 */}
            <Col xs={24} md={12} lg={6}>
              <Form.Item label={`音量: ${config.ttsConfig.volume > 0 ? '+' : ''}${config.ttsConfig.volume}dB`}>
                <Slider
                  min={-50}
                  max={50}
                  step={5}
                  value={config.ttsConfig.volume}
                  onChange={(value) => updateTtsConfig({ volume: value })}
                />
              </Form.Item>
            </Col>
          </Row>

          {/* 输出格式 */}
          <Form.Item label="输出格式">
            <Radio.Group
              value={config.ttsConfig.responseFormat}
              onChange={(e) => updateTtsConfig({ responseFormat: e.target.value })}
            >
              <Radio value="mp3">MP3</Radio>
              <Radio value="wav">WAV</Radio>
            </Radio.Group>
          </Form.Item>
        </Card>
      )}

      {/* 音频播放器 */}
      {generationResult && (
        <Card title="播放控制" size="small">
          <div className="flex items-center justify-between mb-3">
            <Title level={4} className="mb-0">生成结果</Title>
            <Text type="secondary">
              引擎: {generationResult.engineUsed}
            </Text>
          </div>
          
          <Space size="middle" className="w-full">
            <Button
              type="primary"
              shape="circle"
              size="large"
              icon={isPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
              onClick={handlePlay}
            />
            
            <div className="flex-1">
              <Text className="block mb-1">{generationResult.fileName}</Text>
              <div className="w-full bg-gray-200 rounded-full h-2">
                <div className="bg-blue-600 h-2 rounded-full" style={{ width: '0%' }}></div>
              </div>
            </div>
            
            <Button
              icon={<DownloadOutlined />}
              onClick={handleDownload}
              title="下载音频"
            />
          </Space>
          
          <audio
            ref={audioRef}
            src={`data:audio/${config.ttsConfig.responseFormat};base64,${generationResult.audioData}`}
            onEnded={handleAudioEnded}
            onPlay={() => setIsPlaying(true)}
            onPause={() => setIsPlaying(false)}
          />
        </Card>
      )}
    </div>
  );
};

export default TtsGenerator;