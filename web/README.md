# EasyVoice Frontend

基于 React + TypeScript + Tailwind CSS 构建的现代化语音合成前端应用。

## 功能特性

### 🎤 语音合成
- 支持多种 TTS 引擎：Edge TTS、OpenAI TTS、豆包 TTS、Kokoro TTS
- 实时语音参数调节：语速、音调、音量
- 多种音频格式输出：MP3、WAV
- 在线播放和下载功能

### 🧠 智能分析
- LLM 驱动的文本分析
- 自动语言检测
- 情感色调识别
- 智能语音推荐
- 多引擎分析支持

### ⚙️ LLM 配置
- 支持 OpenAI 和豆包模型
- 灵活的 API 配置管理
- 连接测试功能
- 多配置切换

### 📱 响应式设计
- 移动端友好界面
- 现代化 UI 设计
- 流畅的用户体验

## 技术栈

- **框架**: React 18 + TypeScript
- **状态管理**: Zustand
- **样式**: Tailwind CSS
- **图标**: Lucide React
- **HTTP 客户端**: Axios
- **构建工具**: Vite

## 快速开始

### 环境要求

- Node.js >= 16
- npm >= 8

### 安装依赖

```bash
npm install
```

### 环境配置

1. 复制环境变量模板：
```bash
cp .env.example .env
```

2. 编辑 `.env` 文件，配置后端 API 地址：
```env
VITE_API_URL=http://localhost:5000/api
```

### 启动开发服务器

```bash
npm run dev
```

应用将在 `http://localhost:5173` 启动。

### 构建生产版本

```bash
npm run build
```

构建文件将输出到 `dist` 目录。

## 项目结构

```
src/
├── components/          # React 组件
│   ├── TtsGenerator.tsx    # TTS 生成组件
│   ├── TextAnalysis.tsx    # 文本分析组件
│   └── LlmConfiguration.tsx # LLM 配置组件
├── services/            # API 服务
│   └── api.ts              # API 接口定义
├── stores/              # 状态管理
│   └── useAppStore.ts      # 应用状态 Store
├── App.tsx              # 主应用组件
├── main.tsx             # 应用入口
└── index.css            # 全局样式
```

## API 接口

### TTS 相关
- `POST /api/tts/generate` - 生成语音
- `POST /api/tts/intelligent` - 智能语音生成
- `GET /api/tts/voices` - 获取语音列表

### LLM 分析
- `POST /api/llm/analyze` - 文本分析
- `POST /api/llm/recommend-voice` - 语音推荐

### LLM 配置
- `GET /api/llm/configurations` - 获取配置列表
- `PUT /api/llm/configuration` - 更新配置
- `POST /api/llm/test-configuration` - 测试配置

## 使用说明

### 1. 语音合成

1. 在文本框中输入要转换的文本
2. 点击设置按钮调整语音参数
3. 选择合适的语音和输出格式
4. 点击"生成语音"按钮
5. 播放或下载生成的音频

### 2. 智能分析

1. 确保已配置 LLM 服务
2. 输入要分析的文本
3. 选择分析深度和偏好引擎
4. 点击"分析文本"或"推荐语音"
5. 查看分析结果并应用推荐

### 3. LLM 配置

1. 点击"添加配置"按钮
2. 选择模型类型（OpenAI 或豆包）
3. 填写模型名称、API 端点和密钥
4. 测试连接确保配置正确
5. 保存并设为活跃配置

## 开发指南

### 添加新组件

1. 在 `src/components/` 目录下创建新组件
2. 使用 TypeScript 和 Tailwind CSS
3. 通过 Zustand store 管理状态
4. 在 `App.tsx` 中集成组件

### 状态管理

使用 Zustand 进行状态管理，主要状态包括：
- TTS 配置
- LLM 配置
- 分析结果
- 错误信息

### 样式规范

- 使用 Tailwind CSS 工具类
- 保持响应式设计
- 遵循现有的设计系统

## 部署

### 使用 Nginx

1. 构建项目：`npm run build`
2. 将 `dist` 目录内容复制到 Nginx 静态文件目录
3. 配置 Nginx 反向代理到后端 API

### 使用 Docker

```dockerfile
FROM node:16-alpine as builder
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

## 贡献

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

MIT License

## 支持

如有问题或建议，请提交 Issue 或联系开发团队。
