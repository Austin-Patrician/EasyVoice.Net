// AppFooter.tsx
import React from "react";
import { Typography } from "antd";

const AppFooter: React.FC = () => (
  <footer style={{ textAlign: "center", padding: "48px 0", borderTop: "1px solid #f0f0f0", background: "#fafcff" }}>
    <Typography.Title level={4}>
      🎵 EasyVoice - 智能文本转语音平台
    </Typography.Title>
    <Typography.Text type="secondary" style={{ fontSize: 16 }}>
      © 2024 EasyVoice. 基于 React + TypeScript + Ant Design 精心构建
    </Typography.Text>
    <div style={{ marginTop: 16, color: "#bfbfbf", fontSize: 14 }}>
      <span style={{ margin: "0 16px" }}>🚀 高性能</span>
      <span style={{ margin: "0 16px" }}>🎨 现代化UI</span>
      <span style={{ margin: "0 16px" }}>🔊 高质量语音</span>
      <span style={{ margin: "0 16px" }}>🤖 AI驱动</span>
    </div>
  </footer>
);

export default AppFooter;
