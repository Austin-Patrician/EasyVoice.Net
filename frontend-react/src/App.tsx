import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { Layout } from './components/Layout';
import { HomePage, GeneratePage, AboutPage, NotFoundPage, RealTimePage } from './pages';
import RealtimeDialog from './pages/RealtimeDialog';

// Antd主题配置
const theme = {
  token: {
    colorPrimary: '#3b82f6',
    borderRadius: 8,
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
  },
};

function App() {
  return (
    <ConfigProvider theme={theme} locale={zhCN}>
      <Router>
        <Layout>
          <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/generate" element={<GeneratePage />} />
          <Route path="/realtime" element={<RealTimePage />} />
          <Route path="/realtime-dialog" element={<RealtimeDialog />} />
          <Route path="/about" element={<AboutPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
        </Layout>
      </Router>
    </ConfigProvider>
  );
}

export default App;
