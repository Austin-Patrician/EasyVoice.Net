// VoiceSettingsPanel.tsx
import React from "react";
import { Card, Typography, Tabs, Form, Select, Input, Button, Slider, Space } from "antd";
import { PauseCircleOutlined, PlayCircleOutlined } from "@ant-design/icons";

const { TabPane } = Tabs;

interface VoiceSettingsPanelProps {
  config: any;
  updateTtsConfig: (cfg: any) => void;
  updateLlmConfig: (cfg: any) => void;
  handlePreview: () => void;
  previewAudio: string | null;
  isPreviewPlaying: boolean;
  togglePreviewPlay: () => void;
  previewAudioRef: React.RefObject<HTMLAudioElement | null>;
}

const tabContentStyle = { maxHeight: 400, overflow: "auto" };

const VoiceSettingsPanel: React.FC<VoiceSettingsPanelProps> = ({
  config,
  updateTtsConfig,
  updateLlmConfig,
  handlePreview,
  previewAudio,
  isPreviewPlaying,
  togglePreviewPlay,
  previewAudioRef,
}) => {
  return (
    <Card>
      <Typography.Title level={3}>
        🎵 语音设置
      </Typography.Title>
      <Typography.Text type="secondary">
        配置语音参数，选择最适合的声音效果
      </Typography.Text>
      <Tabs defaultActiveKey="preset" style={{ marginTop: 24 }}>
        <TabPane tab="🎤 预设语音" key="preset">
          <div style={tabContentStyle}>
            <Form layout="vertical">
              <Form.Item label="🌍 语言选择">
                <Select
                  value={config.ttsConfig.language}
                  onChange={(value) => updateTtsConfig({ language: value })}
                  size="large"
                >
                  <Select.Option value="zh-CN">🇨🇳 中文 (普通话)</Select.Option>
                  <Select.Option value="en-US">🇺🇸 英语 (美式)</Select.Option>
                </Select>
              </Form.Item>
              <Form.Item label="🎭 语音角色">
                <Select
                  value={config.ttsConfig.voice}
                  onChange={(value) => updateTtsConfig({ voice: value })}
                  size="large"
                >
                  <Select.Option value="zh-CN-YunxiNeural">👨 云希 (温和男声)</Select.Option>
                  <Select.Option value="zh-CN-XiaoxiaoNeural">👩 晓晓 (甜美女声)</Select.Option>
                  <Select.Option value="zh-CN-YunyangNeural">👨 云扬 (磁性男声)</Select.Option>
                  <Select.Option value="zh-CN-XiaoyiNeural">👩 晓伊 (清新女声)</Select.Option>
                </Select>
              </Form.Item>
              <Form.Item label={`⚡ 语速调节: ${config.ttsConfig.speed}x` } style={{ width: "95%" }}>
                <Slider
                  min={0.5}
                  max={2.0}
                  step={0.1}
                  value={config.ttsConfig.speed}
                  onChange={(value) => updateTtsConfig({ speed: value })}
                  marks={{
                    0.5: "慢",
                    1.0: "正常",
                    1.5: "快",
                    2.0: "很快",
                  }}
                  style={{ margin: "0 8px" }}
                />
              </Form.Item>
              <Form.Item label={`🔊 音量控制: ${config.ttsConfig.volume}%`} style={{ width: "95%" }}>
                <Slider
                  min={0}
                  max={100}
                  value={config.ttsConfig.volume}
                  onChange={(value) => updateTtsConfig({ volume: value })}
                  marks={{
                    0: "静",
                    50: "中",
                    100: "大",
                  }}
                  style={{ margin: "0 8px" }}
                />
              </Form.Item>
            </Form>
          </div>
        </TabPane>
        <TabPane tab="🤖 AI配置" key="llm">
          <div style={{ ...tabContentStyle, paddingRight: 16 }}>
            <Form layout="vertical">
              <Form.Item label="🔧 AI服务商">
                <Select
                  value={config.llmConfig?.provider || "openai"}
                  onChange={(value) => updateLlmConfig({ provider: value as "openai" | "doubao" })}
                  size="large"
                >
                  <Select.Option value="doubao">🚀 豆包 (字节跳动)</Select.Option>
                  <Select.Option value="openai">🧠 OpenAI (GPT)</Select.Option>
                </Select>
              </Form.Item>
              <Form.Item label="🌐 API地址">
                <Input
                  value={config.llmConfig?.apiUrl || ""}
                  onChange={(e) => updateLlmConfig({ apiUrl: e.target.value })}
                  placeholder="https://api.example.com/v1"
                  size="large"
                />
              </Form.Item>
              <Form.Item label="🔑 API密钥">
                <Input.Password
                  value={config.llmConfig?.apiKey || ""}
                  onChange={(e) => updateLlmConfig({ apiKey: e.target.value })}
                  placeholder="请输入您的API Key"
                  size="large"
                />
              </Form.Item>
              <Form.Item label="🎯 模型名称">
                <Input
                  value={config.llmConfig?.model || ""}
                  onChange={(e) => updateLlmConfig({ model: e.target.value })}
                  placeholder="gpt-3.5-turbo 或 doubao-pro-4k"
                  size="large"
                />
              </Form.Item>
              <Form.Item label="🎵 试听文本">
                <Input.TextArea
                  value={config.llmConfig?.previewText || "这是一段测试文本，用于验证语音效果"}
                  onChange={(e) => updateLlmConfig({ previewText: e.target.value })}
                  rows={3}
                  placeholder="输入试听文本，测试语音效果"
                />
              </Form.Item>
              <Form.Item>
                <Space>
                  <Button
                    onClick={handlePreview}
                    size="large"
                  >
                    🎧 试听
                  </Button>
                  {previewAudio && (
                    <Button
                      icon={isPreviewPlaying ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
                      onClick={togglePreviewPlay}
                      size="large"
                      type="primary"
                    >
                      {isPreviewPlaying ? "⏸️ 暂停" : "▶️ 播放"}
                    </Button>
                  )}
                </Space>
              </Form.Item>
              {previewAudio && (
                <div>
                  <audio ref={previewAudioRef} src={previewAudio} />
                </div>
              )}
            </Form>
          </div>
        </TabPane>
      </Tabs>
    </Card>
  );
};

export default VoiceSettingsPanel;
