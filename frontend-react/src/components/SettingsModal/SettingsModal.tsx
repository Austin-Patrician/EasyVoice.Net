import React, { useState } from 'react';
import {
  Modal,
  Form,
  Input,
  Select,
  Tabs,
  Button,
  Space,
  Typography,
  Divider,
  Alert,
  Slider,
  Row,
  Col,
} from 'antd';
import { Settings, Key, Globe, Zap } from 'lucide-react';
import { useAudioConfigStore } from '../../stores/audioConfigStore';
import type { LLMSettings } from '../../types';

const { Title, Text } = Typography;
const { Option } = Select;
const { TabPane } = Tabs;

interface SettingsModalProps {
  open: boolean;
  onClose: () => void;
  llmSettings: LLMSettings;
  onSave: (settings: LLMSettings) => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({ open, onClose, llmSettings, onSave }) => {
  const [form] = Form.useForm();
  const [activeTab, setActiveTab] = useState('openai');
  const { updateLLMConfiguration } = useAudioConfigStore();

  const handleSave = () => {
    form.validateFields().then((values) => {
      const updatedSettings = {
        ...llmSettings,
        [activeTab]: values,
      };
      onSave(updatedSettings);
      // 更新统一的LLM配置
      updateLLMConfiguration();
    });
  };

  const handleCancel = () => {
    form.resetFields();
    onClose();
  };

  return (
    <Modal
      title={
        <div className="flex items-center space-x-2">
          <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-purple-500 rounded-lg flex items-center justify-center">
            <Settings className="w-4 h-4 text-white" />
          </div>
          <span className="text-lg font-semibold">LLM 语音配置</span>
        </div>
      }
      open={open}
      onCancel={handleCancel}
      width={600}
      footer={
        <Space>
          <Button onClick={handleCancel}>取消</Button>
          <Button type="primary" onClick={handleSave}>
            保存配置
          </Button>
        </Space>
      }
      className="settings-modal"
    >
      <div className="space-y-4">
        <Alert
          message="LLM 语音配置"
          description="配置 OpenAI 和豆包的 API 参数，用于高质量语音合成"
          type="info"
          showIcon
          className="mb-4"
        />

        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          className="custom-tabs"
        >
          <TabPane
            tab={
              <span className="flex items-center space-x-2">
                <Globe className="w-4 h-4" />
                <span>OpenAI</span>
              </span>
            }
            key="openai"
          >
            <Form
              form={form}
              layout="vertical"
              initialValues={llmSettings?.openai || {}}
              className="space-y-4"
            >
              <Form.Item
                label="Base URL"
                name="baseUrl"
                rules={[
                  { required: true, message: '请输入 Base URL' },
                  { type: 'url', message: '请输入有效的 URL' },
                ]}
              >
                <Input
                  placeholder="https://api.openai.com/v1"
                  prefix={<Globe className="w-4 h-4 text-gray-400" />}
                />
              </Form.Item>

              <Form.Item
                label="API Key"
                name="apiKey"
                rules={[{ required: true, message: '请输入 API Key' }]}
              >
                <Input.Password
                  placeholder="sk-..."
                  prefix={<Key className="w-4 h-4 text-gray-400" />}
                />
              </Form.Item>

              <Form.Item
                label="模型"
                name="model"
                rules={[{ required: true, message: '请选择模型' }]}
              >
                <Select placeholder="选择 OpenAI 模型">
                  <Option value="tts-1">TTS-1 (标准)</Option>
                  <Option value="tts-1-hd">TTS-1-HD (高清)</Option>
                </Select>
              </Form.Item>

              <Form.Item
                label="语音"
                name="voice"
                rules={[{ required: true, message: '请选择语音' }]}
              >
                <Select placeholder="选择语音">
                  <Option value="alloy">Alloy</Option>
                  <Option value="echo">Echo</Option>
                  <Option value="fable">Fable</Option>
                  <Option value="onyx">Onyx</Option>
                  <Option value="nova">Nova</Option>
                  <Option value="shimmer">Shimmer</Option>
                </Select>
              </Form.Item>

              <Divider orientation="left">音频控制</Divider>
              
              <Row gutter={16}>
                <Col span={8}>
                  <Form.Item
                    label="语速"
                    name="speed"
                    initialValue={1.0}
                  >
                    <Slider
                      min={0.25}
                      max={4.0}
                      step={0.25}
                      marks={{
                        0.25: '0.25x',
                        1: '1x',
                        4: '4x'
                      }}
                    />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item
                    label="音调"
                    name="pitch"
                    initialValue={1.0}
                  >
                    <Slider
                      min={0.5}
                      max={2.0}
                      step={0.1}
                      marks={{
                        0.5: '0.5x',
                        1: '1x',
                        2: '2x'
                      }}
                    />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item
                    label="音量"
                    name="volume"
                    initialValue={0.5}
                  >
                    <Slider
                      min={0}
                      max={1}
                      step={0.1}
                      marks={{
                        0: '0%',
                        0.5: '50%',
                        1: '100%'
                      }}
                    />
                  </Form.Item>
                </Col>
              </Row>
            </Form>
          </TabPane>

          <TabPane
            tab={
              <span className="flex items-center space-x-2">
                <Zap className="w-4 h-4" />
                <span>豆包</span>
              </span>
            }
            key="doubao"
          >
            <Form
              form={form}
              layout="vertical"
              initialValues={llmSettings?.doubao || {}}
              className="space-y-4"
            >
              <Form.Item
                label="App ID"
                name="appId"
                rules={[{ required: true, message: '请输入 App ID' }]}
              >
                <Input placeholder="输入豆包 App ID" />
              </Form.Item>

              <Form.Item
                label="Access Token"
                name="accessToken"
                rules={[{ required: true, message: '请输入 Access Token' }]}
              >
                <Input.Password
                  placeholder="输入 Access Token"
                  prefix={<Key className="w-4 h-4 text-gray-400" />}
                />
              </Form.Item>

              <Form.Item
                label="Cluster"
                name="cluster"
                rules={[{ required: true, message: '请输入 Cluster' }]}
              >
                <Input placeholder="输入 Cluster" />
              </Form.Item>

              <Form.Item
                label="Endpoint"
                name="endpoint"
                rules={[
                  { required: true, message: '请输入 Endpoint' },
                  { type: 'url', message: '请输入有效的 URL' },
                ]}
              >
                <Input
                  placeholder="https://..."
                  prefix={<Globe className="w-4 h-4 text-gray-400" />}
                />
              </Form.Item>

              <Form.Item
                label="音频编码"
                name="audioEncoding"
                rules={[{ required: true, message: '请选择音频编码' }]}
              >
                <Select placeholder="选择音频编码格式">
                  <Option value="mp3">MP3</Option>
                  <Option value="wav">WAV</Option>
                  <Option value="pcm">PCM</Option>
                </Select>
              </Form.Item>

              <Divider orientation="left">音频控制</Divider>
              
              <Row gutter={16}>
                <Col span={8}>
                  <Form.Item
                    label="语速"
                    name="speed"
                    initialValue={1.0}
                  >
                    <Slider
                      min={0.25}
                      max={4.0}
                      step={0.25}
                      marks={{
                        0.25: '0.25x',
                        1: '1x',
                        4: '4x'
                      }}
                    />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item
                    label="音调"
                    name="pitch"
                    initialValue={1.0}
                  >
                    <Slider
                      min={0.5}
                      max={2.0}
                      step={0.1}
                      marks={{
                        0.5: '0.5x',
                        1: '1x',
                        2: '2x'
                      }}
                    />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item
                    label="音量"
                    name="volume"
                    initialValue={0.5}
                  >
                    <Slider
                      min={0}
                      max={1}
                      step={0.1}
                      marks={{
                        0: '0%',
                        0.5: '50%',
                        1: '100%'
                      }}
                    />
                  </Form.Item>
                </Col>
              </Row>
            </Form>
          </TabPane>
        </Tabs>

        <Divider />
        
        <div className="text-xs text-gray-500 space-y-1">
          <p>• 配置信息将安全存储在本地</p>
          <p>• API Key 等敏感信息不会上传到服务器</p>
          <p>• 请确保 API 配置的正确性以获得最佳体验</p>
        </div>
      </div>
    </Modal>
  );
};