import React, { useState, useRef } from 'react';
import {
  Layout,
  Typography,
  Card,
  Form,
  Input,
  Button,
  Select,
  Slider,
  Upload,
  Space,
  Row,
  Col,
  Tabs,
  Progress,
  message,
} from 'antd';
import type { TabsProps } from 'antd';
import {
  UploadOutlined,
  SoundOutlined,
  ReloadOutlined,
  PlayCircleOutlined,
  PauseCircleOutlined,
  FastForwardOutlined,
  FastBackwardOutlined,
  DownloadOutlined,
} from '@ant-design/icons';
import { useAppStore } from './stores/useAppStore';
import './App.css';

const { Content, Footer } = Layout;
const { Title, Text } = Typography;
const { TextArea } = Input;
const { Option } = Select;
const { TabPane } = Tabs;

interface AudioItem {
  id: string;
  name: string;
  url: string;
  duration: number;
  createdAt: Date;
}

function App() {
  const { config, inputText, isGenerating, updateTtsConfig, updateLlmConfig, setInputText, setIsGenerating } = useAppStore();
  
  // 音频播放状态
  const [currentAudio, setCurrentAudio] = useState<string | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
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
      message.success('文件上传成功');
    };
    reader.readAsText(file);
    return false; // 阻止默认上传行为
  };
  
  // 生成语音
  const handleGenerate = async () => {
    if (!inputText.trim()) {
      message.error('请输入要转换的文本');
      return;
    }
    
    setIsGenerating(true);
    try {
      // 模拟API调用
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      const newAudio: AudioItem = {
        id: Date.now().toString(),
        name: `语音_${Date.now()}`,
        url: 'data:audio/mp3;base64,', // 这里应该是实际的音频数据
        duration: 30,
        createdAt: new Date()
      };
      
      setCurrentAudio(newAudio.url);
      setAudioList(prev => [newAudio, ...prev]);
      message.success('语音生成成功');
    } catch (error) {
      message.error('生成失败，请重试');
    } finally {
      setIsGenerating(false);
    }
  };
  
  // 重置配置
  const handleReset = () => {
    setInputText('');
    setCurrentAudio(null);
    setIsPlaying(false);
    setAudioList([]);
    message.success('配置已重置');
  };
  
  // 试听功能
  const handlePreview = async () => {
    if (!config.llmConfig?.previewText?.trim()) {
      message.error('请输入试听文本');
      return;
    }
    
    try {
      // 模拟试听API调用
      await new Promise(resolve => setTimeout(resolve, 1000));
      setPreviewAudio('data:audio/mp3;base64,'); // 模拟音频数据
      message.success('试听音频生成成功');
    } catch (error) {
      message.error('试听失败，请重试');
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
  
  // Tabs配置
  const tabItems: TabsProps['items'] = [
    {
      key: 'preset',
      label: '预设语音',
      children: (
        <Form layout="vertical">
          <Form.Item label="语言">
            <Select value={config.ttsConfig.language} onChange={(value) => updateTtsConfig({ language: value })}>
              <Select.Option value="zh-CN">中文</Select.Option>
              <Select.Option value="en-US">英语</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label="语音">
            <Select value={config.ttsConfig.voice} onChange={(value) => updateTtsConfig({ voice: value })}>
              <Select.Option value="zh-CN-YunxiNeural">云希 (男声)</Select.Option>
              <Select.Option value="zh-CN-XiaoxiaoNeural">晓晓 (女声)</Select.Option>
              <Select.Option value="zh-CN-YunyangNeural">云扬 (男声)</Select.Option>
              <Select.Option value="zh-CN-XiaoyiNeural">晓伊 (女声)</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label={`语速: ${config.ttsConfig.speed}x`}>
            <Slider
              min={0.5}
              max={2.0}
              step={0.1}
              value={config.ttsConfig.speed}
              onChange={(value) => updateTtsConfig({ speed: value })}
            />
          </Form.Item>
          
          <Form.Item label={`音量: ${config.ttsConfig.volume}%`}>
            <Slider
              min={0}
              max={100}
              value={config.ttsConfig.volume}
              onChange={(value) => updateTtsConfig({ volume: value })}
            />
          </Form.Item>
        </Form>
      ),
    },
    {
      key: 'llm',
      label: 'LLM 配置',
      children: (
        <Form layout="vertical">
          <Form.Item label="选择类型">
            <Select value={config.llmConfig?.provider || 'openai'} onChange={(value) => updateLlmConfig({ provider: value as 'openai' | 'doubao' })}>
              <Select.Option value="doubao">豆包</Select.Option>
              <Select.Option value="openai">OpenAI</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label="API URL">
            <Input
              value={config.llmConfig?.apiUrl || ''}
              onChange={(e) => updateLlmConfig({ apiUrl: e.target.value })}
              placeholder="请输入API URL"
            />
          </Form.Item>
          
          <Form.Item label="API Key">
            <Input.Password
              value={config.llmConfig?.apiKey || ''}
              onChange={(e) => updateLlmConfig({ apiKey: e.target.value })}
              placeholder="请输入API Key"
            />
          </Form.Item>
          
          <Form.Item label="模型">
            <Input
              value={config.llmConfig?.model || ''}
              onChange={(e) => updateLlmConfig({ model: e.target.value })}
              placeholder="请输入模型名称"
            />
          </Form.Item>
          
          <Form.Item label="试听文本">
            <Input.TextArea
              value={config.llmConfig?.previewText || '这是一段测试文本'}
              onChange={(e) => updateLlmConfig({ previewText: e.target.value })}
              rows={2}
              placeholder="请输入试听文本"
            />
          </Form.Item>
          
          <Form.Item>
            <Space>
              <Button onClick={handlePreview}>试听</Button>
              {previewAudio && (
                <Button
                  icon={isPreviewPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
                  onClick={togglePreviewPlay}
                >
                  {isPreviewPlaying ? '暂停' : '播放'}
                </Button>
              )}
            </Space>
          </Form.Item>
          
          {/* 试听播放器 */}
          {previewAudio && (
            <div className="mt-4 p-4 bg-gray-50 rounded">
              <div className="flex items-center space-x-2 mb-2">
                <Button
                  size="small"
                  icon={isPreviewPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
                  onClick={togglePreviewPlay}
                />
                <Slider className="flex-1" min={0} max={100} value={0} />
                <Typography.Text className="text-sm">0:00 / 0:30</Typography.Text>
              </div>
              <audio ref={previewAudioRef} src={previewAudio} />
            </div>
          )}
        </Form>
      ),
    },
  ];
  
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

  return (
    <Layout className="min-h-screen bg-gray-50">
      <Content className="max-w-6xl mx-auto px-4 py-8">
        {/* 1. 主标题 */}
        <div className="text-center mb-12">
          <Title level={1} className="text-blue-600 mb-2">
            文本转语音
          </Title>
          <Title level={2} className="text-gray-600 font-normal">
            将您的文本一键转换为自然流畅的语音
          </Title>
        </div>
        
        {/* 2. 文本输入卡片 和 3. 语音设置卡片 */}
        <Row gutter={[50, 50]} className="mb-12">
          {/* 2. 文本输入卡片 */}
          <Col xs={24} lg={14}>
            <Card title="文本输入" className="h-full shadow-lg">
              <Form layout="vertical">
                <Form.Item label="输入文本" className="mb-6">
                  <TextArea
                    value={inputText}
                    onChange={(e) => setInputText(e.target.value)}
                    placeholder="请输入要转换为语音的文本..."
                    rows={8}
                    showCount
                    maxLength={5000}
                  />
                </Form.Item>
                
                <div className="border-t pt-4">
                  <Form.Item label="文件上传" className="mb-0">
                    <Upload
                      beforeUpload={handleFileUpload}
                      accept=".txt,.md"
                      showUploadList={false}
                    >
                      <Button icon={<UploadOutlined />} block size="large" className="h-12">
                        上传文本文件 (.txt, .md)
                      </Button>
                    </Upload>
                  </Form.Item>
                </div>
              </Form>
            </Card>
          </Col>
          
          {/* 3. 语音设置卡片 */}
          <Col xs={24} lg={10}>
            <Card title="语音设置" className="h-full shadow-lg">
              <Tabs defaultActiveKey="preset" items={tabItems} />
            </Card>
          </Col>
        </Row>
        
        {/* 4. 生成和重置按钮 */}
        <div className="text-center mb-8">
          <Space size="large">
            <Button
              type="primary"
              size="large"
              icon={<SoundOutlined />}
              onClick={handleGenerate}
              loading={isGenerating}
              disabled={!inputText.trim()}
              className="h-14 px-8 bg-blue-600 hover:bg-blue-700 border-blue-600 hover:border-blue-700 shadow-lg"
            >
              {isGenerating ? '生成中...' : '生成语音'}
            </Button>
            <Button
              size="large"
              icon={<ReloadOutlined />}
              onClick={handleReset}
              className="h-14 px-8 bg-gray-100 hover:bg-gray-200 text-gray-700 border-gray-300 hover:border-gray-400 shadow-lg"
            >
              重置配置
            </Button>
          </Space>
        </div>
        
        {/* 生成进度 */}
        {isGenerating && (
          <div className="mb-12 max-w-md mx-auto">
            <Progress percent={50} status="active" strokeColor="#2563eb" className="mb-2" />
            <div className="text-center text-gray-600">正在生成语音，请稍候...</div>
          </div>
        )}
        
        {/* 5. 音频播放器 */}
        {currentAudio && (
          <Card title="音频播放器" className="mb-12 shadow-lg">
            <div className="flex items-center justify-center space-x-4 mb-4">
              <Button
                icon={<FastBackwardOutlined />}
                onClick={fastBackward}
                size="large"
              />
              <Button
                type="primary"
                icon={isPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
                onClick={togglePlay}
                size="large"
                shape="circle"
              />
              <Button
                icon={<FastForwardOutlined />}
                onClick={fastForward}
                size="large"
              />
            </div>
            
            <div className="mb-4">
              <Slider
                min={0}
                max={duration}
                value={currentTime}
                onChange={(value) => {
                  if (audioRef.current) {
                    audioRef.current.currentTime = value;
                    setCurrentTime(value);
                  }
                }}
              />
            </div>
            
            <div className="text-center">
              <Text>{Math.floor(currentTime / 60)}:{String(Math.floor(currentTime % 60)).padStart(2, '0')} / {Math.floor(duration / 60)}:{String(Math.floor(duration % 60)).padStart(2, '0')}</Text>
            </div>
            
            <audio
              ref={audioRef}
              src={currentAudio}
              onTimeUpdate={() => {
                if (audioRef.current) {
                  setCurrentTime(audioRef.current.currentTime);
                }
              }}
              onLoadedMetadata={() => {
                if (audioRef.current) {
                  setDuration(audioRef.current.duration);
                }
              }}
              onEnded={() => setIsPlaying(false)}
            />
          </Card>
        )}
        
        {/* 6. 下载列表 */}
        {audioList.length > 0 && (
          <Card title={`历史记录 (${audioList.length})`} className="mb-12 shadow-lg">
            <div className="space-y-4">
              {audioList.map((item) => (
                <div key={item.id} className="flex items-center justify-between p-4 bg-gray-50 rounded">
                  <div>
                    <Text strong>{item.name}</Text>
                    <br />
                    <Text type="secondary" className="text-sm">
                      {item.createdAt.toLocaleString()}
                    </Text>
                  </div>
                  <Space>
                    <Button
                      icon={<PlayCircleOutlined />}
                      onClick={() => {
                        setCurrentAudio(item.url);
                        setIsPlaying(false);
                      }}
                    >
                      播放
                    </Button>
                    <Button
                      icon={<DownloadOutlined />}
                      onClick={() => {
                        const link = document.createElement('a');
                        link.href = item.url;
                        link.download = `${item.name}.mp3`;
                        link.click();
                      }}
                    >
                      下载
                    </Button>
                  </Space>
                </div>
              ))}
            </div>
          </Card>
        )}
      </Content>
      
      {/* 7. Footer */}
      <Footer className="text-center bg-white border-t">
        <div className="max-w-6xl mx-auto px-4">
          <p className="mb-2">© 2024 EasyVoice. 基于 React + TypeScript + Ant Design 构建</p>
          <p className="text-gray-600">支持 Edge TTS、OpenAI TTS、豆包 TTS 等多种语音合成引擎</p>
        </div>
      </Footer>
    </Layout>
  );
}

export default App;
