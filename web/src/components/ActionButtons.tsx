// ActionButtons.tsx
import React from "react";
import { Button, Space } from "antd";
import { SoundOutlined, ReloadOutlined } from "@ant-design/icons";

interface ActionButtonsProps {
  isGenerating: boolean;
  inputText: string;
  handleGenerate: () => void;
  handleReset: () => void;
}

const ActionButtons: React.FC<ActionButtonsProps> = ({
  isGenerating,
  inputText,
  handleGenerate,
  handleReset,
}) => {
  return (
    <div style={{ textAlign: "center", marginBottom: 32, padding: "32px 0" }}>
      <Space size="large">
        <Button
          type="primary"
          size="large"
          icon={<SoundOutlined />}
          onClick={handleGenerate}
          loading={isGenerating}
          disabled={!inputText.trim()}
        >
          {isGenerating ? "🎵 生成中..." : "🚀 生成语音"}
        </Button>
        <Button
          size="large"
          icon={<ReloadOutlined />}
          onClick={handleReset}
        >
          🔄 重置配置
        </Button>
      </Space>
    </div>
  );
};

export default ActionButtons;
