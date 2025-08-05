import React from 'react';
import { Link } from 'react-router-dom';
import { Button, Typography, Space } from 'antd';
import { Home, ArrowLeft, Search, Volume2 } from 'lucide-react';

const { Title, Paragraph } = Typography;

export const NotFoundPage: React.FC = () => {
  return (
    <div className="min-h-screen flex items-center justify-center px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full text-center">
        {/* 404 Illustration */}
        <div className="mb-8">
          <div className="relative">
            {/* Large 404 Text */}
            <div className="text-9xl font-bold text-gray-200 select-none">
              404
            </div>
            
            {/* Floating Icons */}
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="relative">
                <Volume2 className="w-16 h-16 text-blue-500 animate-bounce" />
                <div className="absolute -top-2 -right-2 w-6 h-6 bg-red-500 rounded-full flex items-center justify-center">
                  <span className="text-white text-xs font-bold">!</span>
                </div>
              </div>
            </div>
            
            {/* Decorative Elements */}
            <div className="absolute top-4 left-4 w-3 h-3 bg-blue-400 rounded-full animate-pulse" />
            <div className="absolute top-8 right-8 w-2 h-2 bg-purple-400 rounded-full animate-pulse delay-300" />
            <div className="absolute bottom-8 left-8 w-4 h-4 bg-green-400 rounded-full animate-pulse delay-700" />
            <div className="absolute bottom-4 right-4 w-2 h-2 bg-yellow-400 rounded-full animate-pulse delay-500" />
          </div>
        </div>

        {/* Error Message */}
        <div className="mb-8">
          <Title level={2} className="!mb-4 !text-gray-800">
            页面未找到
          </Title>
          <Paragraph className="!text-lg !text-gray-600 !mb-6">
            抱歉，您访问的页面不存在或已被移动。
            请检查URL是否正确，或返回首页继续浏览。
          </Paragraph>
        </div>

        {/* Action Buttons */}
        <Space direction="vertical" size="middle" className="w-full">
          <Link to="/" className="w-full">
            <Button 
              type="primary" 
              size="large" 
              icon={<Home className="w-5 h-5" />}
              className="w-full h-12 text-lg font-medium"
            >
              返回首页
            </Button>
          </Link>
          
          <Button 
            size="large" 
            icon={<ArrowLeft className="w-5 h-5" />}
            onClick={() => window.history.back()}
            className="w-full h-12 text-lg font-medium"
          >
            返回上一页
          </Button>
          
          <Link to="/generate" className="w-full">
            <Button 
              size="large" 
              icon={<Volume2 className="w-5 h-5" />}
              className="w-full h-12 text-lg font-medium"
            >
              开始语音生成
            </Button>
          </Link>
        </Space>

        {/* Help Text */}
        <div className="mt-12 pt-8 border-t border-gray-200">
          <Paragraph className="!text-sm !text-gray-500 !mb-4">
            如果您认为这是一个错误，请联系我们：
          </Paragraph>
          <Space wrap>
            <a 
              href="mailto:support@vox.com" 
              className="text-blue-600 hover:text-blue-800 text-sm"
            >
              support@vox.com
            </a>
            <span className="text-gray-300">|</span>
            <Link 
              to="/about" 
              className="text-blue-600 hover:text-blue-800 text-sm"
            >
              关于我们
            </Link>
          </Space>
        </div>

        {/* Search Suggestion */}
        <div className="mt-8 p-4 bg-blue-50 rounded-lg">
          <div className="flex items-center justify-center mb-2">
            <Search className="w-5 h-5 text-blue-600 mr-2" />
            <span className="text-blue-800 font-medium">寻找其他内容？</span>
          </div>
          <Paragraph className="!text-sm !text-blue-700 !mb-0">
            您可以浏览我们的主要功能页面，或查看帮助文档了解更多信息。
          </Paragraph>
        </div>
      </div>
    </div>
  );
};