// AudioHistoryList.tsx
import React from "react";
import { Card, Typography, Button, List, Avatar, Space } from "antd";
import { PlayCircleOutlined, DownloadOutlined } from "@ant-design/icons";

interface AudioItem {
  id: string;
  name: string;
  url: string;
  duration: number;
  createdAt: Date;
}

interface AudioHistoryListProps {
  audioList: AudioItem[];
  setCurrentAudio: (url: string) => void;
  setIsPlaying: (playing: boolean) => void;
}

const AudioHistoryList: React.FC<AudioHistoryListProps> = ({
  audioList,
  setCurrentAudio,
  setIsPlaying,
}) => {
  if (!audioList.length) return null;
  return (
    <Card
      bordered={false}
      style={{
        borderRadius: 16,
        background: "#fff",
        boxShadow: "0 4px 16px rgba(0,0,0,0.06)",
        marginBottom: 24,
      }}
      bodyStyle={{ padding: 32 }}
    >
      <Typography.Title level={3} style={{ marginBottom: 8 }}>
        📚 历史记录
      </Typography.Title>
      <Typography.Text type="secondary" style={{ fontSize: 15 }}>
        您之前生成的所有语音文件，可随时播放或下载
      </Typography.Text>
      <List
        itemLayout="horizontal"
        dataSource={audioList}
        style={{ marginTop: 24 }}
        split={true}
        renderItem={(item, index) => (
          <List.Item
            style={{
              padding: "18px 0",
              borderBottom: index === audioList.length - 1 ? "none" : "1px solid #f0f0f0",
              alignItems: "center",
            }}
            actions={[
              <Button
                icon={<PlayCircleOutlined />}
                onClick={() => {
                  setCurrentAudio(item.url);
                  setIsPlaying(false);
                }}
                type="text"
                size="large"
                style={{ color: "#1677ff" }}
              >
                播放
              </Button>,
              <Button
                icon={<DownloadOutlined />}
                onClick={() => {
                  const link = document.createElement("a");
                  link.href = item.url;
                  link.download = `${item.name}.mp3`;
                  link.click();
                }}
                type="text"
                size="large"
                style={{ color: "#52c41a" }}
              >
                下载
              </Button>,
            ]}
          >
            <List.Item.Meta
              avatar={
                <Avatar
                  style={{
                    background: "linear-gradient(135deg, #1677ff 0%, #52c41a 100%)",
                    color: "#fff",
                    fontWeight: 600,
                  }}
                  size={40}
                >
                  {index + 1}
                </Avatar>
              }
              title={
                <Typography.Text strong style={{ fontSize: 16 }}>
                  {item.name}
                </Typography.Text>
              }
              description={
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  {new Date(item.createdAt).toLocaleString()} · 时长 {Math.floor(item.duration / 60)}:{String(Math.floor(item.duration % 60)).padStart(2, "0")}
                </Typography.Text>
              }
            />
          </List.Item>
        )}
      />
    </Card>
  );
};

export default AudioHistoryList;
