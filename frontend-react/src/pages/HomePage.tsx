import React, { useState, useRef, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Button, Card, Collapse, Space, Typography, Row, Col } from 'antd';
import {
  PlayCircle,
  PauseCircle,
  Volume2,
  Mic,
  Globe,
  Zap,
  Download,
  Shield,
  ArrowRight,
  CheckCircle,
} from 'lucide-react';
import type { VoiceSegment } from '../types';

const { Title, Paragraph, Text } = Typography;
const { Panel } = Collapse;

// 演示语音段落数据
const demoSegments: VoiceSegment[] = [
  {
    voice: '欢迎使用EasyVoice，',
    start: 0,
    end: 2,
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20female%20avatar%20with%20blue%20theme&image_size=square',
    color: 'from-blue-500 to-blue-600',
    textColor: 'text-blue-600',
  },
  {
    voice: '专业的AI语音合成平台。',
    start: 2,
    end: 4.5,
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20male%20avatar%20with%20purple%20theme&image_size=square',
    color: 'from-purple-500 to-purple-600',
    textColor: 'text-purple-600',
  },
  {
    voice: '让文字变成自然流畅的语音。',
    start: 4.5,
    end: 7,
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20female%20avatar%20with%20green%20theme&image_size=square',
    color: 'from-green-500 to-green-600',
    textColor: 'text-green-600',
  },
];

// 功能特点数据
const features = [
  {
    icon: Volume2,
    title: '高质量语音',
    description: '采用先进的AI技术，生成自然流畅、接近真人的语音效果',
    color: 'from-blue-500 to-blue-600',
  },
  {
    icon: Globe,
    title: '多语言支持',
    description: '支持中文、英文、日文、韩文等多种语言的语音合成',
    color: 'from-green-500 to-green-600',
  },
  {
    icon: Mic,
    title: '多种音色',
    description: '提供男声、女声等多种音色选择，满足不同场景需求',
    color: 'from-purple-500 to-purple-600',
  },
  {
    icon: Zap,
    title: '快速生成',
    description: '高效的处理速度，几秒钟即可完成语音合成',
    color: 'from-yellow-500 to-orange-500',
  },
  {
    icon: Download,
    title: '便捷下载',
    description: '支持多种音频格式下载，方便在各种设备上使用',
    color: 'from-red-500 to-pink-500',
  },
  {
    icon: Shield,
    title: '安全可靠',
    description: '严格的数据保护措施，确保用户隐私和数据安全',
    color: 'from-indigo-500 to-blue-600',
  },
];

// FAQ数据
const faqData = [
  {
    key: '1',
    label: 'EasyVoice支持哪些语言？',
    children: (
      <p className="text-gray-600">
        EasyVoice支持中文（普通话）、英文、日文、韩文、法文、德文、西班牙文、意大利文、葡萄牙文、俄文等多种语言的语音合成。
      </p>
    ),
  },
  {
    key: '2',
    label: '生成的语音质量如何？',
    children: (
      <p className="text-gray-600">
        我们采用最先进的AI语音合成技术，生成的语音自然流畅，接近真人发音效果。支持调节语速、音调等参数，满足不同需求。
      </p>
    ),
  },
  {
    key: '3',
    label: '有文本长度限制吗？',
    children: (
      <p className="text-gray-600">
        单次生成支持最多10,000个字符的文本。对于更长的文本，建议分段处理以获得最佳效果。
      </p>
    ),
  },
  {
    key: '4',
    label: '生成的音频可以商用吗？',
    children: (
      <p className="text-gray-600">
        生成的音频文件可以用于个人和商业用途。请确保遵守相关法律法规，不要用于违法或侵权活动。
      </p>
    ),
  },
  {
    key: '5',
    label: '如何获得更好的语音效果？',
    children: (
      <div className="text-gray-600">
        <p>为了获得最佳语音效果，建议：</p>
        <ul className="list-disc list-inside mt-2 space-y-1">
          <li>使用标准的文本格式，避免特殊符号</li>
          <li>合理使用标点符号来控制语音节奏</li>
          <li>根据内容选择合适的音色和语速</li>
          <li>对于专业术语，可以使用拼音标注</li>
        </ul>
      </div>
    ),
  },
];

export const HomePage: React.FC = () => {
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [currentSegment, setCurrentSegment] = useState(0);
  const audioRef = useRef<HTMLAudioElement>(null);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);

  // 模拟音频播放
  const handlePlayPause = () => {
    if (isPlaying) {
      setIsPlaying(false);
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    } else {
      setIsPlaying(true);
      intervalRef.current = setInterval(() => {
        setCurrentTime((prev) => {
          const newTime = prev + 0.1;
          if (newTime >= 7) {
            setIsPlaying(false);
            setCurrentTime(0);
            setCurrentSegment(0);
            if (intervalRef.current) {
              clearInterval(intervalRef.current);
            }
            return 0;
          }
          return newTime;
        });
      }, 100);
    }
  };

  // 更新当前语音段落
  useEffect(() => {
    const segment = demoSegments.findIndex(
      (seg) => currentTime >= seg.start && currentTime < seg.end
    );
    if (segment !== -1) {
      setCurrentSegment(segment);
    }
  }, [currentTime]);

  // 清理定时器
  useEffect(() => {
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, []);

  return (
    <div className="min-h-screen">
      {/* Hero Section */}
      <section className="relative py-20 px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-7xl text-center">
          <Title level={1} className="!text-5xl !font-bold !mb-6">
            <span className="bg-gradient-to-r from-blue-600 via-purple-600 to-blue-800 bg-clip-text text-transparent">
              让文字变成声音
            </span>
          </Title>
          <Paragraph className="!text-xl !text-gray-600 !mb-8 max-w-3xl mx-auto">
            EasyVoice是一个专业的AI语音合成平台，采用先进的人工智能技术，
            将您的文字转换为自然流畅的语音。支持多种语言和音色，满足各种场景需求。
          </Paragraph>
          
          {/* CTA Buttons */}
          <Space size="large" className="mb-16">
            <Link to="/generate">
              <Button 
                type="primary" 
                size="large" 
                className="h-12 px-8 text-lg font-medium"
                icon={<ArrowRight className="w-5 h-5" />}
              >
                立即体验
              </Button>
            </Link>
            <Button 
              size="large" 
              className="h-12 px-8 text-lg font-medium"
              onClick={handlePlayPause}
              icon={isPlaying ? <PauseCircle className="w-5 h-5" /> : <PlayCircle className="w-5 h-5" />}
            >
              {isPlaying ? '暂停演示' : '播放演示'}
            </Button>
          </Space>

          {/* Voice Demo Player */}
          <Card className="max-w-4xl mx-auto shadow-lg border-0 bg-white/80 backdrop-blur-sm">
            <div className="p-6">
              <Title level={4} className="!mb-6 !text-gray-800">
                语音演示
              </Title>
              
              {/* Progress Bar */}
              <div className="mb-6">
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div 
                    className="bg-gradient-to-r from-blue-500 to-purple-500 h-2 rounded-full transition-all duration-100"
                    style={{ width: `${(currentTime / 7) * 100}%` }}
                  />
                </div>
                <div className="flex justify-between text-sm text-gray-500 mt-2">
                  <span>{currentTime.toFixed(1)}s</span>
                  <span>7.0s</span>
                </div>
              </div>

              {/* Voice Segments */}
              <div className="space-y-4">
                {demoSegments.map((segment, index) => (
                  <div
                    key={index}
                    className={`flex items-center space-x-4 p-4 rounded-lg transition-all duration-300 ${
                      currentSegment === index
                        ? 'bg-gradient-to-r from-blue-50 to-purple-50 border-2 border-blue-200'
                        : 'bg-gray-50 border border-gray-200'
                    }`}
                  >
                    <div className="relative">
                      <img
                        src={segment.avatar}
                        alt={`Voice ${index + 1}`}
                        className="w-12 h-12 rounded-full object-cover"
                      />
                      {currentSegment === index && isPlaying && (
                        <div className="absolute -inset-1 rounded-full bg-gradient-to-r from-blue-500 to-purple-500 opacity-75 animate-pulse" />
                      )}
                    </div>
                    <div className="flex-1">
                      <Text 
                        className={`text-lg font-medium ${
                          currentSegment === index ? segment.textColor : 'text-gray-700'
                        }`}
                      >
                        {segment.voice}
                      </Text>
                      <div className="text-sm text-gray-500">
                        {segment.start.toFixed(1)}s - {segment.end.toFixed(1)}s
                      </div>
                    </div>
                    {currentSegment === index && isPlaying && (
                      <Volume2 className="w-5 h-5 text-blue-500 animate-pulse" />
                    )}
                  </div>
                ))}
              </div>
            </div>
          </Card>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 px-4 sm:px-6 lg:px-8 bg-white">
        <div className="mx-auto max-w-7xl">
          <div className="text-center mb-16">
            <Title level={2} className="!mb-4">
              为什么选择 EasyVoice？
            </Title>
            <Paragraph className="!text-lg !text-gray-600 max-w-2xl mx-auto">
              我们提供业界领先的AI语音合成技术，为您带来卓越的语音体验
            </Paragraph>
          </div>
          
          <Row gutter={[32, 32]}>
            {features.map((feature, index) => {
              const Icon = feature.icon;
              return (
                <Col xs={24} sm={12} lg={8} key={index}>
                  <Card 
                    className="h-full border-0 shadow-md hover:shadow-lg transition-all duration-300 hover:-translate-y-1"
                    bodyStyle={{ padding: '2rem' }}
                  >
                    <div className="text-center">
                      <div className={`inline-flex p-4 rounded-full bg-gradient-to-r ${feature.color} mb-4`}>
                        <Icon className="w-8 h-8 text-white" />
                      </div>
                      <Title level={4} className="!mb-3">
                        {feature.title}
                      </Title>
                      <Paragraph className="!text-gray-600 !mb-0">
                        {feature.description}
                      </Paragraph>
                    </div>
                  </Card>
                </Col>
              );
            })}
          </Row>
        </div>
      </section>

      {/* Stats Section */}
      <section className="py-20 px-4 sm:px-6 lg:px-8 bg-gradient-to-r from-blue-600 to-purple-600">
        <div className="mx-auto max-w-7xl">
          <Row gutter={[32, 32]} className="text-center">
            <Col xs={24} sm={8}>
              <div className="text-white">
                <Title level={2} className="!text-white !mb-2">
                  10+
                </Title>
                <Text className="text-blue-100 text-lg">
                  支持语言
                </Text>
              </div>
            </Col>
            <Col xs={24} sm={8}>
              <div className="text-white">
                <Title level={2} className="!text-white !mb-2">
                  50+
                </Title>
                <Text className="text-blue-100 text-lg">
                  音色选择
                </Text>
              </div>
            </Col>
            <Col xs={24} sm={8}>
              <div className="text-white">
                <Title level={2} className="!text-white !mb-2">
                  99.9%
                </Title>
                <Text className="text-blue-100 text-lg">
                  服务可用性
                </Text>
              </div>
            </Col>
          </Row>
        </div>
      </section>

      {/* FAQ Section */}
      <section className="py-20 px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-4xl">
          <div className="text-center mb-16">
            <Title level={2} className="!mb-4">
              常见问题
            </Title>
            <Paragraph className="!text-lg !text-gray-600">
              解答您关于EasyVoice的疑问
            </Paragraph>
          </div>
          
          <Collapse 
            size="large"
            className="bg-white border-0 shadow-sm"
            expandIcon={({ isActive }) => (
              <CheckCircle className={`w-5 h-5 transition-transform ${isActive ? 'rotate-90' : ''}`} />
            )}
          >
            {faqData.map((item) => (
              <Panel 
                key={item.key} 
                header={<span className="text-lg font-medium">{item.label}</span>}
                className="border-b border-gray-100 last:border-b-0"
              >
                {item.children}
              </Panel>
            ))}
          </Collapse>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 px-4 sm:px-6 lg:px-8 bg-gray-50">
        <div className="mx-auto max-w-4xl text-center">
          <Title level={2} className="!mb-6">
            准备开始了吗？
          </Title>
          <Paragraph className="!text-lg !text-gray-600 !mb-8">
            立即体验EasyVoice的强大功能，让您的文字变成动听的声音
          </Paragraph>
          <Link to="/generate">
            <Button 
              type="primary" 
              size="large" 
              className="h-14 px-12 text-lg font-medium"
              icon={<ArrowRight className="w-6 h-6" />}
            >
              开始使用
            </Button>
          </Link>
        </div>
      </section>
    </div>
  );
};