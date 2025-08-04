// TextInputArea.tsx
import React from 'react';
import { Form, Input, Divider, Typography, Button, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';

const { TextArea } = Input;

interface TextInputAreaProps {
  inputText: string;
  setInputText: (text: string) => void;
  handleFileUpload: (file: File) => boolean | Promise<void>;
}

const TextInputArea: React.FC<TextInputAreaProps> = ({
  inputText,
  setInputText,
  handleFileUpload,
}) => {
  return (
    <div>
      <Typography.Title level={3}>
        📝 文本输入
      </Typography.Title>
      <Typography.Text type="secondary">
        在下方输入您要转换的文本内容，支持最多5000字符
      </Typography.Text>
      <Form layout="vertical" style={{ marginTop: 24 }}>
        <Form.Item>
          <TextArea
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            placeholder="请输入要转换为语音的文本内容...\n\n支持多段落文本，系统会自动处理换行和标点符号，确保语音输出自然流畅。"
            rows={14}
            showCount
            maxLength={5000}
          />
        </Form.Item>
        <Divider>
          <Typography.Text type="secondary">或者</Typography.Text>
        </Divider>
        <div>
          <Typography.Title level={4} style={{ marginBottom: 8 }}>
            📁 上传文本文件
          </Typography.Title>
          <Typography.Text type="secondary">
            支持 .txt 和 .md 格式文件，文件内容将自动填充到上方文本框
          </Typography.Text>
          <Upload
            beforeUpload={handleFileUpload}
            accept=".txt,.md"
            showUploadList={false}
            style={{ width: "100%", marginTop: 16 }}
          >
            <Button
              icon={<UploadOutlined />}
              size="large"
              block
            >
              点击选择文件或拖拽文件到此处
            </Button>
          </Upload>
        </div>
      </Form>
    </div>
  );
};

export default TextInputArea;
