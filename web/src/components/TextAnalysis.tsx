import React, { useState } from 'react';
import { useAppStore } from '../stores/useAppStore';
import { EasyVoiceApi } from '../services/api';
import type { LlmAnalysisRequest, VoiceRecommendationRequest } from '../services/api';
import {
  Card,
  Form,
  Input,
  Button,
  Space,
  Typography,
  Upload,
  message,
  Divider,
  Tag,
  Row,
  Col,
  Statistic,
  Progress,
  Select
} from 'antd';
import {
  FileTextOutlined,
  ExperimentOutlined,
  LoadingOutlined,
  DownloadOutlined,
  UploadOutlined,
  FundProjectionScreenOutlined,
  BookOutlined,
  ClockCircleOutlined,
  BulbOutlined,
  AimOutlined,
  TrophyOutlined,
  ExclamationCircleOutlined,
  CheckCircleOutlined
} from '@ant-design/icons';

const { TextArea } = Input;
const { Title, Text, Paragraph } = Typography;
const { Option } = Select;

const TextAnalysis: React.FC = () => {
  const {
    config,
    inputText,
    isAnalyzing,
    analysisResult,
    voiceRecommendation,
    error,
    setIsAnalyzing,
    setAnalysisResult,
    setVoiceRecommendation,
    setError,
    clearError,
    updateTtsConfig,
  } = useAppStore();

  const [analysisDepth, setAnalysisDepth] = useState<'Basic' | 'Detailed' | 'Comprehensive'>('Detailed');
  const [preferredEngine, setPreferredEngine] = useState<'OpenAI' | 'Doubao' | 'Edge' | 'Kokoro'>('Edge');

  const handleAnalyzeText = async () => {
    if (!inputText.trim()) {
      setError('请输入要分析的文本');
      return;
    }

    if (!config.activeLlmConfig) {
      setError('请先配置LLM服务');
      return;
    }

    clearError();
    setIsAnalyzing(true);

    try {
      const request: LlmAnalysisRequest = {
        text: inputText,
        options: {
          useMultiEngine: true,
          depth: analysisDepth,
        },
      };

      const response = await EasyVoiceApi.analyzeText(request);

      if (response.success && response.data) {
        setAnalysisResult({
          detectedLanguage: response.data.detectedLanguage,
          emotionTone: response.data.emotionTone,
          recommendedEngine: response.data.recommendedEngine,
          recommendedVoice: response.data.recommendedVoice,
          confidence: response.data.confidence,
        });
      } else {
        setError(response.message || '分析失败');
      }
    } catch (error) {
      console.error('Text analysis error:', error);
      setError('分析过程中发生错误，请稍后重试');
    } finally {
      setIsAnalyzing(false);
    }
  };

  const handleRecommendVoice = async () => {
    if (!inputText.trim()) {
      setError('请输入要分析的文本');
      return;
    }

    if (!config.activeLlmConfig) {
      setError('请先配置LLM服务');
      return;
    }

    clearError();
    setIsAnalyzing(true);

    try {
      const request: VoiceRecommendationRequest = {
        text: inputText,
        preferredEngine,
      };

      const response = await EasyVoiceApi.recommendVoice(request);

      if (response.success && response.data) {
        setVoiceRecommendation({
          recommendedEngine: response.data.recommendedEngine,
          recommendedVoice: response.data.recommendedVoice,
          reason: response.data.reason,
          confidence: response.data.confidence,
        });
      } else {
        setError(response.message || '推荐失败');
      }
    } catch (error) {
      console.error('Voice recommendation error:', error);
      setError('推荐过程中发生错误，请稍后重试');
    } finally {
      setIsAnalyzing(false);
    }
  };

  const applyRecommendation = () => {
    if (voiceRecommendation) {
      updateTtsConfig({ voice: voiceRecommendation.recommendedVoice });
    }
  };

  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.8) return 'text-green-600';
    if (confidence >= 0.6) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getConfidenceIcon = (confidence: number) => {
    if (confidence >= 0.8) return <CheckCircleOutlined className="text-green-600" />;
    if (confidence >= 0.6) return <ExclamationCircleOutlined className="text-yellow-600" />;
    return <ExclamationCircleOutlined className="text-red-600" />;
  };

  if (!config.enableLlmAnalysis && !config.enableVoiceRecommendation) {
    return null;
  }

  return (
    <Card className="w-full">
      <div className="flex items-center justify-between mb-6">
        <Title level={2} className="mb-0 flex items-center gap-2">
          <ExperimentOutlined className="text-purple-600" />
          智能分析
        </Title>
      </div>

      {/* 分析选项 */}
      <Row gutter={[16, 16]} className="mb-6">
        {config.enableLlmAnalysis && (
          <Col xs={24} md={12}>
            <Card size="small" title={
              <span>
                <AimOutlined className="text-blue-600 mr-2" />
                文本分析
              </span>
            }>
              <Space direction="vertical" className="w-full">
                <Form.Item label="分析深度" className="mb-2">
                  <Select
                    value={analysisDepth}
                    onChange={(value) => setAnalysisDepth(value)}
                    className="w-full"
                  >
                    <Option value="Basic">基础分析</Option>
                    <Option value="Detailed">详细分析</Option>
                    <Option value="Comprehensive">全面分析</Option>
                  </Select>
                </Form.Item>
                
                <Button
                  type="primary"
                  onClick={handleAnalyzeText}
                  disabled={isAnalyzing || !inputText.trim() || !config.activeLlmConfig}
                  loading={isAnalyzing}
                  icon={isAnalyzing ? <LoadingOutlined /> : <ExperimentOutlined />}
                  block
                >
                  {isAnalyzing ? '分析中...' : '分析文本'}
                </Button>
              </Space>
            </Card>
          </Col>
        )}

        {config.enableVoiceRecommendation && (
          <Col xs={24} md={12}>
            <Card size="small" title={
              <span>
                <BulbOutlined className="text-yellow-600 mr-2" />
                语音推荐
              </span>
            }>
              <Space direction="vertical" className="w-full">
                <Form.Item label="偏好引擎" className="mb-2">
                  <Select
                    value={preferredEngine}
                    onChange={(value) => setPreferredEngine(value)}
                    className="w-full"
                  >
                    <Option value="Edge">Edge TTS</Option>
                    <Option value="OpenAI">OpenAI TTS</Option>
                    <Option value="Doubao">豆包 TTS</Option>
                    <Option value="Kokoro">Kokoro TTS</Option>
                  </Select>
                </Form.Item>
                
                <Button
                  type="default"
                  onClick={handleRecommendVoice}
                  disabled={isAnalyzing || !inputText.trim() || !config.activeLlmConfig}
                  loading={isAnalyzing}
                  icon={isAnalyzing ? <LoadingOutlined /> : <BulbOutlined />}
                  block
                >
                  {isAnalyzing ? '推荐中...' : '推荐语音'}
                </Button>
              </Space>
            </Card>
          </Col>
        )}
      </Row>

      {/* 错误信息 */}
      {error && (
        <div className="mb-4">
          <Card>
            <div className="flex items-center gap-2 text-red-600">
              <ExclamationCircleOutlined />
              <Text type="danger" strong>错误</Text>
            </div>
            <Paragraph className="mt-2 mb-0">
              <Text type="danger">{error}</Text>
            </Paragraph>
          </Card>
        </div>
      )}

      {/* 分析结果 */}
      {analysisResult && (
        <div className="mb-6">
          <Card title={
            <span>
              <FundProjectionScreenOutlined className="text-blue-600 mr-2" />
              分析结果
            </span>
          }>
            <Row gutter={[16, 16]}>
              <Col xs={24} sm={12} lg={8}>
                <Card size="small">
                  <Statistic title="检测语言" value={analysisResult.detectedLanguage} />
                </Card>
              </Col>
              
              <Col xs={24} sm={12} lg={8}>
                <Card size="small">
                  <Statistic title="情感色调" value={analysisResult.emotionTone} />
                </Card>
              </Col>
              
              <Col xs={24} sm={12} lg={8}>
                <Card size="small">
                  <Statistic title="推荐引擎" value={analysisResult.recommendedEngine} />
                </Card>
              </Col>
              
              <Col xs={24} sm={12} lg={8}>
                <Card size="small">
                  <Statistic title="推荐语音" value={analysisResult.recommendedVoice} />
                </Card>
              </Col>
              
              <Col xs={24} sm={12} lg={8}>
                <Card size="small">
                  <div className="text-center">
                    <div className="text-sm text-gray-600 mb-1">置信度</div>
                    <div className={`font-semibold flex items-center justify-center gap-1 ${getConfidenceColor(analysisResult.confidence)}`}>
                      {getConfidenceIcon(analysisResult.confidence)}
                      {(analysisResult.confidence * 100).toFixed(1)}%
                    </div>
                    <Progress 
                      percent={analysisResult.confidence * 100} 
                      size="small"
                      status={analysisResult.confidence > 0.8 ? 'success' : 'normal'}
                      className="mt-2"
                    />
                  </div>
                </Card>
              </Col>
            </Row>
          </Card>
        </div>
      )}

      {/* 语音推荐结果 */}
      {voiceRecommendation && (
        <Card title={
          <span>
            <BulbOutlined className="text-yellow-600 mr-2" />
            语音推荐
          </span>
        }>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12}>
              <Card size="small">
                <Statistic title="推荐引擎" value={voiceRecommendation.recommendedEngine} />
              </Card>
            </Col>
            
            <Col xs={24} sm={12}>
              <Card size="small">
                <Statistic title="推荐语音" value={voiceRecommendation.recommendedVoice} />
              </Card>
            </Col>
            
            <Col xs={24}>
              <Card size="small">
                <div className="text-center">
                  <div className="text-sm text-gray-600 mb-1">置信度</div>
                  <div className={`font-semibold flex items-center justify-center gap-1 ${getConfidenceColor(voiceRecommendation.confidence)}`}>
                    {getConfidenceIcon(voiceRecommendation.confidence)}
                    {(voiceRecommendation.confidence * 100).toFixed(1)}%
                  </div>
                  <Progress 
                    percent={voiceRecommendation.confidence * 100} 
                    size="small"
                    status={voiceRecommendation.confidence > 0.8 ? 'success' : 'normal'}
                    className="mt-2"
                  />
                </div>
              </Card>
            </Col>
          </Row>
          
          <Divider />
          
          <Card size="small" title="推荐理由" className="mb-4">
            <Paragraph>{voiceRecommendation.reason}</Paragraph>
          </Card>
          
          <Button
            type="primary"
            onClick={applyRecommendation}
            icon={<CheckCircleOutlined />}
            className="bg-yellow-600 hover:bg-yellow-700 border-yellow-600 hover:border-yellow-700"
          >
            应用推荐
          </Button>
        </Card>
      )}
    </Card>
  );
};

export default TextAnalysis;