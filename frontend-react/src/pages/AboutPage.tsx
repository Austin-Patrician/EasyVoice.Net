import React from 'react';
import { Card, Typography, Row, Col, Timeline, Space, Button } from 'antd';
import { Link } from 'react-router-dom';
import {
  Volume2,
  Users,
  Target,
  Award,
  Globe,
  Shield,
  Zap,
  Heart,
  ArrowRight,
  Mail,
  Github,
} from 'lucide-react';

const { Title, Paragraph, Text } = Typography;

// 团队成员数据
const teamMembers = [
  {
    name: '张三',
    role: '创始人 & CEO',
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20CEO%20avatar%20male%20with%20blue%20suit&image_size=square',
    description: '10年AI技术经验，专注于语音合成领域',
  },
  {
    name: '李四',
    role: 'CTO & 技术负责人',
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20CTO%20avatar%20female%20with%20tech%20background&image_size=square',
    description: '深度学习专家，语音技术架构师',
  },
  {
    name: '王五',
    role: '产品经理',
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20product%20manager%20avatar%20male&image_size=square',
    description: '用户体验设计专家，产品策略制定者',
  },
  {
    name: '赵六',
    role: '算法工程师',
    avatar: 'https://trae-api-sg.mchost.guru/api/ide/v1/text_to_image?prompt=professional%20engineer%20avatar%20female%20with%20AI%20background&image_size=square',
    description: 'AI算法优化专家，语音质量提升负责人',
  },
];

// 发展历程数据
const milestones = [
  {
    time: '2024年1月',
    title: '项目启动',
    description: 'EasyVoice项目正式启动，开始技术调研和产品规划',
  },
  {
    time: '2024年3月',
    title: '技术突破',
    description: '完成核心AI语音合成算法开发，实现高质量语音生成',
  },
  {
    time: '2024年6月',
    title: '产品发布',
    description: '正式发布EasyVoice平台，支持多语言语音合成',
  },
  {
    time: '2024年9月',
    title: '功能升级',
    description: '新增AI语音模式，支持更多自定义语音风格',
  },
  {
    time: '2024年12月',
    title: '用户突破',
    description: '用户数量突破10万，日均语音生成量达到100万次',
  },
];

// 核心价值数据
const values = [
  {
    icon: Target,
    title: '专业专注',
    description: '专注于AI语音合成技术，追求极致的语音质量和用户体验',
    color: 'from-blue-500 to-blue-600',
  },
  {
    icon: Users,
    title: '用户至上',
    description: '以用户需求为导向，持续优化产品功能和服务质量',
    color: 'from-green-500 to-green-600',
  },
  {
    icon: Zap,
    title: '创新驱动',
    description: '不断探索前沿技术，推动语音合成技术的发展和应用',
    color: 'from-purple-500 to-purple-600',
  },
  {
    icon: Shield,
    title: '安全可靠',
    description: '严格保护用户隐私，确保数据安全和服务稳定性',
    color: 'from-red-500 to-pink-500',
  },
];

export const AboutPage: React.FC = () => {
  return (
    <div className="min-h-screen py-8 px-4 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl">
        {/* Hero Section */}
        <div className="text-center mb-16">
          <div className="flex justify-center mb-6">
            <div className="flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-r from-blue-600 to-purple-600">
              <Volume2 className="h-8 w-8 text-white" />
            </div>
          </div>
          <Title level={1} className="!mb-6">
            关于 EasyVoice
          </Title>
          <Paragraph className="!text-xl !text-gray-600 max-w-3xl mx-auto">
            EasyVoice是一个专业的AI语音合成平台，致力于为用户提供高质量、自然流畅的语音生成服务。
            我们相信技术的力量能够让沟通变得更加便捷和美好。
          </Paragraph>
        </div>

        {/* 使命愿景 */}
        <Row gutter={[32, 32]} className="mb-20">
          <Col xs={24} md={12}>
            <Card className="h-full border-0 shadow-lg">
              <div className="text-center p-6">
                <div className="inline-flex p-4 rounded-full bg-gradient-to-r from-blue-500 to-blue-600 mb-6">
                  <Target className="w-8 h-8 text-white" />
                </div>
                <Title level={3} className="!mb-4">
                  我们的使命
                </Title>
                <Paragraph className="!text-gray-600 !text-lg">
                  让每个人都能轻松获得高质量的AI语音服务，
                  打破语言和技术壁垒，让沟通变得更加自然和高效。
                </Paragraph>
              </div>
            </Card>
          </Col>
          <Col xs={24} md={12}>
            <Card className="h-full border-0 shadow-lg">
              <div className="text-center p-6">
                <div className="inline-flex p-4 rounded-full bg-gradient-to-r from-purple-500 to-purple-600 mb-6">
                  <Globe className="w-8 h-8 text-white" />
                </div>
                <Title level={3} className="!mb-4">
                  我们的愿景
                </Title>
                <Paragraph className="!text-gray-600 !text-lg">
                  成为全球领先的AI语音合成平台，
                  为世界各地的用户提供最优质的语音技术服务。
                </Paragraph>
              </div>
            </Card>
          </Col>
        </Row>

        {/* 核心价值 */}
        <div className="mb-20">
          <div className="text-center mb-12">
            <Title level={2} className="!mb-4">
              核心价值
            </Title>
            <Paragraph className="!text-lg !text-gray-600">
              指导我们前进的核心理念
            </Paragraph>
          </div>
          
          <Row gutter={[24, 24]}>
            {values.map((value, index) => {
              const Icon = value.icon;
              return (
                <Col xs={24} sm={12} lg={6} key={index}>
                  <Card className="h-full border-0 shadow-md hover:shadow-lg transition-all duration-300 text-center">
                    <div className={`inline-flex p-3 rounded-full bg-gradient-to-r ${value.color} mb-4`}>
                      <Icon className="w-6 h-6 text-white" />
                    </div>
                    <Title level={4} className="!mb-3">
                      {value.title}
                    </Title>
                    <Paragraph className="!text-gray-600 !mb-0">
                      {value.description}
                    </Paragraph>
                  </Card>
                </Col>
              );
            })}
          </Row>
        </div>

        {/* 发展历程 */}
        <div className="mb-20">
          <div className="text-center mb-12">
            <Title level={2} className="!mb-4">
              发展历程
            </Title>
            <Paragraph className="!text-lg !text-gray-600">
              见证我们的成长足迹
            </Paragraph>
          </div>
          
          <Card className="border-0 shadow-lg">
            <Timeline
              mode="left"
              className="p-6"
              items={milestones.map((milestone, index) => ({
                key: index,
                label: <Text strong className="text-blue-600">{milestone.time}</Text>,
                children: (
                  <div>
                    <Title level={4} className="!mb-2">
                      {milestone.title}
                    </Title>
                    <Paragraph className="!text-gray-600 !mb-0">
                      {milestone.description}
                    </Paragraph>
                  </div>
                ),
              }))}
            />
          </Card>
        </div>

        {/* 团队介绍 */}
        <div className="mb-20">
          <div className="text-center mb-12">
            <Title level={2} className="!mb-4">
              团队介绍
            </Title>
            <Paragraph className="!text-lg !text-gray-600">
              专业的团队，专注的精神
            </Paragraph>
          </div>
          
          <Row gutter={[24, 24]}>
            {teamMembers.map((member, index) => (
              <Col xs={24} sm={12} lg={6} key={index}>
                <Card className="border-0 shadow-md hover:shadow-lg transition-all duration-300 text-center">
                  <div className="p-6">
                    <img
                      src={member.avatar}
                      alt={member.name}
                      className="w-20 h-20 rounded-full mx-auto mb-4 object-cover"
                    />
                    <Title level={4} className="!mb-2">
                      {member.name}
                    </Title>
                    <Text className="text-blue-600 font-medium block mb-3">
                      {member.role}
                    </Text>
                    <Paragraph className="!text-gray-600 !text-sm !mb-0">
                      {member.description}
                    </Paragraph>
                  </div>
                </Card>
              </Col>
            ))}
          </Row>
        </div>

        {/* 技术优势 */}
        <div className="mb-20">
          <div className="text-center mb-12">
            <Title level={2} className="!mb-4">
              技术优势
            </Title>
            <Paragraph className="!text-lg !text-gray-600">
              领先的技术实力
            </Paragraph>
          </div>
          
          <Row gutter={[32, 32]}>
            <Col xs={24} lg={12}>
              <Card className="h-full border-0 shadow-lg">
                <div className="p-6">
                  <div className="flex items-center mb-4">
                    <Award className="w-6 h-6 text-blue-600 mr-3" />
                    <Title level={4} className="!mb-0">
                      先进算法
                    </Title>
                  </div>
                  <Paragraph className="!text-gray-600">
                    采用最新的深度学习和神经网络技术，
                    实现高质量的语音合成效果，
                    支持多种语言和音色的自然表达。
                  </Paragraph>
                </div>
              </Card>
            </Col>
            <Col xs={24} lg={12}>
              <Card className="h-full border-0 shadow-lg">
                <div className="p-6">
                  <div className="flex items-center mb-4">
                    <Zap className="w-6 h-6 text-green-600 mr-3" />
                    <Title level={4} className="!mb-0">
                      高效处理
                    </Title>
                  </div>
                  <Paragraph className="!text-gray-600">
                    优化的算法架构和云端计算资源，
                    确保快速响应和高并发处理能力，
                    为用户提供流畅的使用体验。
                  </Paragraph>
                </div>
              </Card>
            </Col>
          </Row>
        </div>

        {/* 联系我们 */}
        <div className="text-center">
          <Card className="border-0 shadow-lg bg-gradient-to-r from-blue-50 to-purple-50">
            <div className="p-8">
              <Title level={2} className="!mb-6">
                联系我们
              </Title>
              <Paragraph className="!text-lg !text-gray-600 !mb-8">
                有任何问题或建议？我们很乐意听到您的声音
              </Paragraph>
              
              <Space size="large" wrap>
                <Button 
                  type="primary" 
                  size="large" 
                  icon={<Mail className="w-5 h-5" />}
                  href="mailto:contact@easyvoice.com"
                >
                  发送邮件
                </Button>
                <Button 
                  size="large" 
                  icon={<Github className="w-5 h-5" />}
                  href="https://github.com/easyvoice"
                  target="_blank"
                >
                  GitHub
                </Button>
                <Link to="/generate">
                  <Button 
                    size="large" 
                    icon={<ArrowRight className="w-5 h-5" />}
                  >
                    开始使用
                  </Button>
                </Link>
              </Space>
              
              <div className="mt-8 pt-6 border-t border-gray-200">
                <Paragraph className="!text-gray-500 !mb-0 flex items-center justify-center">
                  Made with <Heart className="w-4 h-4 text-red-500 mx-2" /> by EasyVoice Team
                </Paragraph>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
};