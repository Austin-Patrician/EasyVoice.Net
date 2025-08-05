import React, { useEffect, useState } from 'react';
import {
  Card,
  Input,
  Button,
  Select,
  Slider,
  Typography,
  Row,
  Col,
  Space,
  Divider,
  Alert,
  Upload,
  message,
  Radio,
  Tooltip,
} from 'antd';
import {
  Settings,
  FileText,
  Upload as UploadIcon,
  Trash2,
  Play,
  Activity,
  Sliders,
  Languages,
  Zap,
  Globe,
  Cpu,
} from 'lucide-react';
import { useAudioConfigStore } from '../stores/audioConfigStore';
import { useGenerationStore } from '../stores/generationStore';
import { apiService } from '../services/api';
import { LANGUAGES, GENDER_OPTIONS, VOICE_MODE_OPTIONS, LLM_PROVIDER_OPTIONS } from '../constants';
import { DownloadList } from '../components/Layout';
import { SettingsModal } from '../components/SettingsModal';
import type { Voice, VoiceMode, LLMProvider } from '../types';

const { TextArea } = Input;
const { Title, Text } = Typography;
const { Option } = Select;
const { Dragger } = Upload;

export const GeneratePage: React.FC = () => {
  const [text, setText] = useState('');
  const [uploadedText, setUploadedText] = useState('');
  const [settingsModalOpen, setSettingsModalOpen] = useState(false);
  
  // Zustand stores
  const {
    config: audioConfig,
    voices,
    updateConfig: updateAudioConfig,
    updateVoiceMode,
    updateLLMProvider,
    updateLLMConfiguration,
    setVoices: loadVoices,
    isLoadingVoices,
    error,
    setError,
  } = useAudioConfigStore();
  
  const {
    audioList,
    addAudioItem,
  } = useGenerationStore();
  
  const [isGenerating, setIsGenerating] = useState(false);

  // 加载语音列表
  useEffect(() => {
    // 模拟加载语音列表
    loadVoices([]);
  }, [loadVoices]);

  // 处理文本输入
  const handleTextChange = (value: string) => {
    setText(value);
    setError(null);
  };

  // 处理语音生成
  const handleGenerate = async () => {
    if (!text.trim()) {
      message.warning('请输入要转换的文本');
      return;
    }

    // 验证配置
    if (audioConfig.voiceMode === 'edge' && !audioConfig.selectedVoice) {
      message.warning('请选择一个语音');
      return;
    }

    if (audioConfig.voiceMode === 'llm') {
      const llmConfig = audioConfig.llmConfiguration[audioConfig.llmProvider];
      if (!llmConfig || (audioConfig.llmProvider === 'openai' && !(llmConfig as import('../types').OpenAIConfig).apiKey)) {
        message.warning('请先配置 LLM 参数');
        setSettingsModalOpen(true);
        return;
      }
      
      // 验证豆包配置
      if (audioConfig.llmProvider === 'doubao') {
        const doubaoConfig = llmConfig as import('../types').DoubaoConfig;
        if (!doubaoConfig.accessToken) {
          message.warning('请先配置豆包 Access Token');
          setSettingsModalOpen(true);
          return;
        }
      }
    }

    try {
      setIsGenerating(true);
      setError(null);

      const requestData = {
        text: text.trim(),
        voiceMode: audioConfig.voiceMode,
        ...(audioConfig.voiceMode === 'edge' ? {
          voice: audioConfig.selectedVoice,
          rate: audioConfig.rate.toString(),
          pitch: audioConfig.pitch.toString(),
          gender: audioConfig.selectedGender,
        } : {
          llmProvider: audioConfig.llmProvider,
          llmConfig: audioConfig.llmConfiguration[audioConfig.llmProvider],
        }),
      };

      const response = await apiService.generateVoice(requestData);
      
      // 添加到音频列表
      addAudioItem({
        taskId: response.id,
        text: text.trim(),
        title: `语音_${new Date().toLocaleString()}`,
        status: 'completed',
        audio: response.audio,
        file: response.file,
        voice: audioConfig.voiceMode === 'edge' ? audioConfig.selectedVoice : audioConfig.llmProvider,
        gender: audioConfig.selectedGender,
      });
      
      message.success('开始生成语音，请在下方查看进度');
      
    } catch (error: any) {
      console.error('Generation failed:', error);
      setError(error.message || '语音生成失败，请重试');
      message.error(error.message || '语音生成失败');
    } finally {
      setIsGenerating(false);
    }
  };

  // 处理文件上传
  const handleFileUpload = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      if (content) {
        setUploadedText(content);
        handleTextChange(content);
        message.success('文件上传成功');
      }
    };
    reader.readAsText(file);
    return false; // 阻止自动上传
  };

  // 获取过滤后的语音列表
  const filteredVoices = voices.filter(voice => {
    const languageMatch = !audioConfig.selectedLanguage || voice.Name.includes(audioConfig.selectedLanguage);
    const genderMatch = !audioConfig.selectedGender || voice.Gender === audioConfig.selectedGender;
    return languageMatch && genderMatch;
  });

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50">
      {/* 顶部标题区域 */}
      <div className="bg-white shadow-sm border-b border-gray-100">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-6">
          <div className="text-center">
            <Title level={2} className="!mb-2 bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
              AI语音生成工作台
            </Title>
            <Text className="text-gray-600">
              支持 Edge 语音和 LLM 语音，提供专业的文本转语音服务
            </Text>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-8">
        {error && (
          <Alert
            message={error}
            type="error"
            showIcon
            closable
            className="mb-6"
            onClose={() => setError(null)}
          />
        )}

        <Row gutter={[32, 32]}>
          {/* 左侧：文本输入区域 */}
          <Col xs={24} lg={12}>
            <Card 
              className="shadow-lg border-0 bg-white/80 backdrop-blur-sm h-full"
              title={
                <div className="flex items-center space-x-2">
                  <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-purple-500 rounded-lg flex items-center justify-center">
                    <FileText className="w-4 h-4 text-white" />
                  </div>
                  <span className="text-lg font-semibold">文本输入</span>
                </div>
              }
            >
              <div className="space-y-4">
                {/* 文件上传 */}
                <Dragger
                  accept=".txt,.docx,.pdf"
                  beforeUpload={handleFileUpload}
                  showUploadList={false}
                  className="border-2 border-dashed border-blue-200 hover:border-blue-400 bg-blue-50/50"
                >
                    <div className="py-8">
                      <UploadIcon className="w-12 h-12 mx-auto text-blue-500 mb-4" />
                      <p className="text-lg font-medium text-gray-700">点击或拖拽文件到此区域</p>
                      <p className="text-sm text-gray-500 mt-2">支持 .txt, .docx, .pdf 格式文件</p>
                    </div>
                  </Dragger>

                {/* 文本输入框 */}
                <div className="relative">
                  <TextArea
                    value={text}
                    onChange={(e) => handleTextChange(e.target.value)}
                    placeholder="请输入要转换为语音的文本内容...\n\n支持中文、英文等多种语言\n最多支持5000个字符"
                    rows={16}
                    showCount
                    maxLength={5000}
                    className="text-base leading-relaxed border-gray-200 focus:border-blue-400 focus:shadow-lg transition-all duration-200"
                  />
                  {text.length > 0 && (
                    <div className="absolute top-4 right-4">
                      <Button
                        type="text"
                        icon={<Trash2 className="w-4 h-4" />}
                        onClick={() => {
                          setUploadedText('');
                          setText('');
                        }}
                        className="text-gray-400 hover:text-red-500"
                      />
                    </div>
                  )}
                </div>
                
                {/* 生成按钮 */}
                <div className="flex justify-center pt-4">
                  <Button
                    type="primary"
                    size="large"
                    icon={<Play className="w-5 h-5" />}
                    onClick={handleGenerate}
                    loading={isGenerating}
                    disabled={!text.trim()}
                    className="h-14 px-12 text-lg font-semibold bg-gradient-to-r from-blue-500 to-purple-500 border-0 hover:from-blue-600 hover:to-purple-600 shadow-lg hover:shadow-xl transition-all duration-200"
                  >
                    {isGenerating ? '正在生成语音...' : '立即生成语音'}
                  </Button>
                </div>
                
                {/* 使用提示 */}
                <div className="mt-6 p-4 bg-gradient-to-r from-blue-50 to-purple-50 rounded-lg border border-blue-200">
                  <div className="flex items-center space-x-2 mb-3">
                    <Activity className="w-4 h-4 text-blue-600" />
                    <Text strong className="text-blue-800">使用提示</Text>
                  </div>
                  <div className="space-y-2 text-sm text-gray-600">
                    <div className="flex items-start space-x-2">
                      <div className="w-2 h-2 bg-blue-500 rounded-full mt-2 flex-shrink-0"></div>
                      <span>支持最多 5,000 个字符的文本转换</span>
                    </div>
                    <div className="flex items-start space-x-2">
                      <div className="w-2 h-2 bg-green-500 rounded-full mt-2 flex-shrink-0"></div>
                      <span>Edge 语音提供快速转换，LLM 语音提供高质量合成</span>
                    </div>
                    <div className="flex items-start space-x-2">
                      <div className="w-2 h-2 bg-purple-500 rounded-full mt-2 flex-shrink-0"></div>
                      <span>生成的音频将自动添加到下载列表</span>
                    </div>
                  </div>
                </div>
              </div>
            </Card>
          </Col>

          {/* 右侧：语音配置区域 */}
          <Col xs={24} lg={12}>
            <Card 
              className="shadow-lg border-0 bg-white/80 backdrop-blur-sm h-full"
              title={
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-2">
                    <div className="w-8 h-8 bg-gradient-to-r from-green-500 to-blue-500 rounded-lg flex items-center justify-center">
                      <Settings className="w-4 h-4 text-white" />
                    </div>
                    <span className="text-lg font-semibold">语音配置</span>
                  </div>
                  {audioConfig.voiceMode === 'llm' && (
                    <Tooltip title="配置 LLM 参数">
                      <Button
                        type="text"
                        icon={<Settings className="w-4 h-4" />}
                        onClick={() => setSettingsModalOpen(true)}
                        className="text-blue-600 hover:text-blue-800"
                      />
                    </Tooltip>
                  )}
                </div>
              }
            >
              <div className="space-y-6">
                {/* 语音模式选择 */}
                <div className="space-y-3">
                  <Text strong className="text-gray-700">语音模式</Text>
                  <Radio.Group
                    value={audioConfig.voiceMode}
                    onChange={(e) => updateVoiceMode(e.target.value)}
                    className="w-full"
                  >
                    <div className="grid grid-cols-2 gap-3">
                      <Radio.Button
                        value="edge"
                        className="h-24 flex items-center justify-center text-center border-2 hover:border-blue-400"
                      >
                        <div className="flex flex-col items-center space-y-1">
                          <Cpu className="w-5 h-5" />
                          <span className="font-medium">Edge 语音</span>
                          <span className="text-xs text-gray-500">快速转换</span>
                        </div>
                      </Radio.Button>
                      <Radio.Button
                        value="llm"
                        className="h-24 flex items-center justify-center text-center border-2 hover:border-purple-400"
                      >
                        <div className="flex flex-col items-center space-y-1">
                          <Zap className="w-5 h-5" />
                          <span className="font-medium">LLM 语音</span>
                          <span className="text-xs text-gray-500">高质量合成</span>
                        </div>
                      </Radio.Button>
                    </div>
                  </Radio.Group>
                </div>

                {/* Edge 语音配置 */}
                {audioConfig.voiceMode === 'edge' && (
                  <div className="space-y-4">
                    <Divider orientation="left">
                      <span className="text-blue-600 font-medium">Edge 语音设置</span>
                    </Divider>
                    <Row gutter={[16, 16]}>
                       <Col xs={24}>
                         <div className="space-y-2">
                           <Text strong className="text-gray-700">语言选择</Text>
                           <Select
                             value={audioConfig.selectedLanguage}
                             onChange={(value) => updateAudioConfig({ selectedLanguage: value })}
                             className="w-full"
                             loading={isLoadingVoices}
                             size="large"
                           >
                             {LANGUAGES.map(lang => (
                               <Option key={lang.code} value={lang.code}>
                                 {lang.name}
                               </Option>
                             ))}
                           </Select>
                         </div>
                       </Col>
                       <Col xs={24}>
                         <div className="space-y-2">
                           <Text strong className="text-gray-700">性别选择</Text>
                           <Select
                             value={audioConfig.selectedGender}
                             onChange={(value) => updateAudioConfig({ selectedGender: value })}
                             className="w-full"
                             size="large"
                           >
                             {GENDER_OPTIONS.map(option => (
                               <Option key={option.value} value={option.value}>
                                 {option.label}
                               </Option>
                             ))}
                           </Select>
                         </div>
                       </Col>
                       <Col xs={24}>
                         <div className="space-y-2">
                           <Text strong className="text-gray-700">语音选择 ({filteredVoices.length}个)</Text>
                           <Select
                             value={audioConfig.selectedVoice}
                             onChange={(value) => updateAudioConfig({ selectedVoice: value })}
                             className="w-full"
                             placeholder="请选择语音"
                             loading={isLoadingVoices}
                             showSearch
                             size="large"
                             filterOption={(input, option) =>
                               String(option?.children || '').toLowerCase().includes(input.toLowerCase())
                             }
                           >
                             {filteredVoices.map(voice => (
                               <Option key={voice.Name} value={voice.Name}>
                                 {voice.Name} ({voice.Gender})
                               </Option>
                             ))}
                           </Select>
                         </div>
                       </Col>
                     </Row>

                     <Row gutter={[16, 16]}>
                        <Col xs={24}>
                          <div className="space-y-4">
                            <Text strong className="text-gray-700">语速调节</Text>
                            <div className="px-4">
                              <Slider
                                min={0.5}
                                max={2.0}
                                step={0.1}
                                value={audioConfig.rate}
                                onChange={(value) => updateAudioConfig({ rate: value })}
                                marks={{
                                  '0.5': '慢',
                                  '1.0': '正常',
                                  '2.0': '快',
                                }}
                              />
                              <div className="text-center mt-2">
                                <Text className="text-blue-600 font-medium">{audioConfig.rate}x</Text>
                              </div>
                            </div>
                          </div>
                        </Col>
                        <Col xs={24}>
                          <div className="space-y-4">
                            <Text strong className="text-gray-700">音调调节</Text>
                            <div className="px-4">
                              <Slider
                                min={0.5}
                                max={2.0}
                                step={0.1}
                                value={audioConfig.pitch}
                                onChange={(value) => updateAudioConfig({ pitch: value })}
                                marks={{
                                  '0.5': '低',
                                  '1.0': '正常',
                                  '2.0': '高',
                                }}
                              />
                              <div className="text-center mt-2">
                                <Text className="text-purple-600 font-medium">{audioConfig.pitch}</Text>
                              </div>
                            </div>
                          </div>
                        </Col>
                        <Col xs={24}>
                          <div className="space-y-4">
                              <Text strong className="text-gray-700">音量调节</Text>
                              <div className="px-4">
                                <Slider
                                  min={0}
                                  max={1}
                                  step={0.1}
                                  value={audioConfig.volume}
                                  onChange={(value) => updateAudioConfig({ volume: value })}
                                  marks={{
                                    '0': '静音',
                                    '0.5': '正常',
                                    '1': '最大',
                                  }}
                                />
                                <div className="text-center mt-2">
                                  <Text className="text-green-600 font-medium">{Math.round(audioConfig.volume * 100)}%</Text>
                                </div>
                              </div>
                            </div>
                          </Col>
                        </Row>
                      </div>
                    )}

                    {/* LLM 语音配置 */}
                    {audioConfig.voiceMode === 'llm' && (
                      <div className="space-y-4">
                        <Divider orientation="left">
                          <span className="text-purple-600 font-medium">LLM 语音设置</span>
                        </Divider>
                        
                        <div className="space-y-4">
                          <div className="space-y-2">
                            <Text strong className="text-gray-700">LLM 提供商</Text>
                            <Select
                              value={audioConfig.llmProvider}
                              onChange={(value) => updateLLMProvider(value)}
                              className="w-full"
                              size="large"
                            >
                              {LLM_PROVIDER_OPTIONS.map(option => (
                                <Option key={option.value} value={option.value}>
                                  {option.label}
                                </Option>
                              ))}
                            </Select>
                          </div>
                          
                          <div className="bg-purple-50 p-4 rounded-lg border border-purple-200">
                            <div className="flex items-start space-x-3">
                              <Zap className="w-5 h-5 text-purple-500 mt-0.5 flex-shrink-0" />
                              <div className="text-left">
                                <Text className="text-purple-700 font-medium block mb-1">
                                  当前提供商：{audioConfig.llmProvider === 'openai' ? 'OpenAI' : '豆包'}
                                </Text>
                                <Text className="text-purple-600 text-sm">
                                  {audioConfig.llmProvider === 'openai' 
                                    ? 'OpenAI 提供高质量的语音合成服务，支持多种语音模型'
                                    : '豆包提供专业的中文语音合成，音质自然流畅'
                                  }
                                </Text>
                              </div>
                            </div>
                          </div>
                          
                          <div className="text-center">
                            <Button
                              type="primary"
                              icon={<Settings className="w-4 h-4" />}
                              onClick={() => setSettingsModalOpen(true)}
                              className="bg-gradient-to-r from-purple-500 to-blue-500 border-0 hover:from-purple-600 hover:to-blue-600"
                              size="large"
                            >
                              配置 LLM 参数
                            </Button>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                </Card>
              </Col>


        </Row>
        
        {/* 下载列表 */}
        <div className="mt-12">
          <DownloadList className="shadow-lg" />
        </div>
      </div>
      
      {/* LLM 设置弹窗 */}
         <SettingsModal
           open={settingsModalOpen}
           onClose={() => setSettingsModalOpen(false)}
           llmConfiguration={audioConfig.llmConfiguration}
           onSave={(provider, config) => {
             updateLLMConfiguration(provider as LLMProvider, config);
             setSettingsModalOpen(false);
           }}
         />
    </div>
  );
};