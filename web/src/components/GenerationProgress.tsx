// GenerationProgress.tsx
import React from "react";
import { Typography, Progress } from "antd";

interface GenerationProgressProps {
  isGenerating: boolean;
}

const GenerationProgress: React.FC<GenerationProgressProps> = ({ isGenerating }) => {
  if (!isGenerating) return null;
  return (
    <div style={{ maxWidth: 480, margin: "0 auto 32px auto", textAlign: "center" }}>
      <Typography.Title level={4}>
        🎵 正在生成语音...
      </Typography.Title>
      <Typography.Text type="secondary">
        请稍候，AI正在为您精心制作高质量语音
      </Typography.Text>
      <div style={{ margin: "24px 0" }}>
        <Progress percent={50} status="active" />
      </div>
    </div>
  );
};

export default GenerationProgress;
