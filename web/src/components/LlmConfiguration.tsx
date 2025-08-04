import React, { useState, useEffect } from 'react';
import { useAppStore } from '../stores/useAppStore';
import { EasyVoiceApi } from '../services/api';
import type { LlmConfiguration } from '../services/api';
import {
  Card,
  Form,
  Input,
  Button,
  Space,
  Typography,
  Switch,
  Select,
  Divider,
  Row,
  Col,
  Modal,
  message,
  Popconfirm,
  Tag,
  Tooltip
} from 'antd';
import {
  SettingOutlined,
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  CheckOutlined,
  CloseOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
  ApiOutlined,
  KeyOutlined,
  GlobalOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
  ExperimentOutlined
} from '@ant-design/icons';

const { Option } = Select;

const LlmConfiguration: React.FC = () => {
  const {
    config,
    updateLlmConfigurations,
    setActiveLlmConfig,
    setError,
    clearError,
  } = useAppStore();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingConfig, setEditingConfig] = useState<LlmConfiguration | null>(null);
  const [testingConfig, setTestingConfig] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, boolean>>({});
  const [showApiKey, setShowApiKey] = useState<Record<string, boolean>>({});
  const [formData, setFormData] = useState<LlmConfiguration>({
    modelType: 'OpenAI',
    modelName: '',
    endpoint: '',
    apiKey: '',
    enabled: true,
  });

  useEffect(() => {
    loadConfigurations();
  }, []);

  const loadConfigurations = async () => {
    try {
      const response = await EasyVoiceApi.getLlmConfigurations();
      if (response.success && response.data) {
        updateLlmConfigurations(response.data);
        
        // 设置第一个启用的配置为活跃配置
        const activeConfig = response.data.find(c => c.enabled);
        if (activeConfig && !config.activeLlmConfig) {
          setActiveLlmConfig(activeConfig);
        }
      }
    } catch (error) {
      console.error('Load configurations error:', error);
      setError('加载配置失败');
    }
  };

  const handleSaveConfiguration = async () => {
    if (!formData.modelName || !formData.endpoint || !formData.apiKey) {
      setError('请填写所有必填字段');
      return;
    }

    clearError();

    try {
      const response = await EasyVoiceApi.updateLlmConfiguration(formData);
      if (response.success) {
        await loadConfigurations();
        setIsModalOpen(false);
        setEditingConfig(null);
        resetForm();
      } else {
        setError(response.message || '保存配置失败');
      }
    } catch (error) {
      console.error('Save configuration error:', error);
      setError('保存配置时发生错误');
    }
  };

  const handleTestConfiguration = async (configToTest: LlmConfiguration) => {
    const configId = `${configToTest.modelType}-${configToTest.modelName}`;
    setTestingConfig(configId);
    
    try {
      const response = await EasyVoiceApi.testLlmConfiguration(configToTest);
      setTestResults(prev => ({
        ...prev,
        [configId]: response.success && response.data === true,
      }));
    } catch (error) {
      console.error('Test configuration error:', error);
      setTestResults(prev => ({
        ...prev,
        [configId]: false,
      }));
    } finally {
      setTestingConfig(null);
    }
  };

  const handleEditConfiguration = (config: LlmConfiguration) => {
    setEditingConfig(config);
    setFormData({ ...config });
    setIsModalOpen(true);
  };

  const handleDeleteConfiguration = async (config: LlmConfiguration) => {
    if (window.confirm('确定要删除这个配置吗？')) {
      try {
        // 这里应该调用删除API，但目前API中没有删除方法
        // 暂时通过禁用来模拟删除
        const updatedConfig = { ...config, enabled: false };
        await EasyVoiceApi.updateLlmConfiguration(updatedConfig);
        await loadConfigurations();
      } catch (error) {
        console.error('Delete configuration error:', error);
        setError('删除配置失败');
      }
    }
  };

  const handleSetActive = (config: LlmConfiguration) => {
    setActiveLlmConfig(config);
  };

  const resetForm = () => {
    setFormData({
      modelType: 'OpenAI',
      modelName: '',
      endpoint: '',
      apiKey: '',
      enabled: true,
    });
  };

  const openAddModal = () => {
    setEditingConfig(null);
    resetForm();
    setIsModalOpen(true);
  };

  const toggleApiKeyVisibility = (configId: string) => {
    setShowApiKey(prev => ({
      ...prev,
      [configId]: !prev[configId],
    }));
  };

  const getDefaultEndpoint = (modelType: 'OpenAI' | 'Doubao') => {
    switch (modelType) {
      case 'OpenAI':
        return 'https://api.openai.com/v1';
      case 'Doubao':
        return 'https://ark.cn-beijing.volces.com/api/v3';
      default:
        return '';
    }
  };

  const getDefaultModelName = (modelType: 'OpenAI' | 'Doubao') => {
    switch (modelType) {
      case 'OpenAI':
        return 'gpt-3.5-turbo';
      case 'Doubao':
        return 'doubao-lite-4k';
      default:
        return '';
    }
  };

  const handleModelTypeChange = (modelType: 'OpenAI' | 'Doubao') => {
    setFormData({
      ...formData,
      modelType,
      endpoint: getDefaultEndpoint(modelType),
      modelName: getDefaultModelName(modelType),
    });
  };

  return (
    <div className="bg-white rounded-lg shadow-lg p-6">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-800 flex items-center gap-2">
          <SettingOutlined className="w-6 h-6 text-green-600" />
          LLM 配置
        </h2>
        <button
          onClick={openAddModal}
          className="bg-green-600 hover:bg-green-700 text-white font-semibold py-2 px-4 rounded-lg transition-colors flex items-center gap-2"
        >
          <PlusOutlined className="w-4 h-4" />
          添加配置
        </button>
      </div>

      {/* 配置列表 */}
      <div className="space-y-4 pr-4">
        {config.llmConfigurations.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <SettingOutlined className="w-12 h-12 mx-auto mb-4 text-gray-300" />
            <p>暂无LLM配置，请添加配置以启用智能分析功能</p>
          </div>
        ) : (
          config.llmConfigurations
            .filter(config => config.enabled)
            .map((llmConfig) => {
              const configId = `${llmConfig.modelType}-${llmConfig.modelName}`;
              const isActive = config.activeLlmConfig?.modelName === llmConfig.modelName && 
                             config.activeLlmConfig?.modelType === llmConfig.modelType;
              const isTesting = testingConfig === configId;
              const testResult = testResults[configId];

              return (
                <div
                  key={configId}
                  className={`p-4 border rounded-lg transition-colors ${
                    isActive ? 'border-green-500 bg-green-50' : 'border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-3 mb-2">
                        <h3 className="text-lg font-semibold text-gray-800">
                          {llmConfig.modelType} - {llmConfig.modelName}
                        </h3>
                        {isActive && (
                          <span className="bg-green-100 text-green-800 text-xs font-medium px-2 py-1 rounded-full">
                            当前使用
                          </span>
                        )}
                        {testResult !== undefined && (
                          <span className={`flex items-center gap-1 text-xs ${
                            testResult ? 'text-green-600' : 'text-red-600'
                          }`}>
                            {testResult ? (
                              <CheckCircleOutlined className="w-3 h-3" />
                            ) : (
                              <CloseCircleOutlined className="w-3 h-3" />
                            )}
                            {testResult ? '连接正常' : '连接失败'}
                          </span>
                        )}
                      </div>
                      
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm text-gray-600">
                        <div>
                          <span className="font-medium">端点:</span> {llmConfig.endpoint}
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="font-medium">API Key:</span>
                          <span className="font-mono">
                            {showApiKey[configId] 
                              ? llmConfig.apiKey 
                              : '••••••••••••••••••••••••••••••••'
                            }
                          </span>
                          <button
                            onClick={() => toggleApiKeyVisibility(configId)}
                            className="text-gray-400 hover:text-gray-600"
                          >
                            {showApiKey[configId] ? (
                              <EyeInvisibleOutlined className="w-4 h-4" />
                            ) : (
                              <EyeOutlined className="w-4 h-4" />
                            )}
                          </button>
                        </div>
                      </div>
                    </div>
                    
                    <div className="flex items-center gap-2 ml-4">
                      {!isActive && (
                        <button
                          onClick={() => handleSetActive(llmConfig)}
                          className="text-green-600 hover:text-green-800 p-1"
                          title="设为活跃配置"
                        >
                          <CheckCircleOutlined className="w-4 h-4" />
                        </button>
                      )}
                      
                      <button
                        onClick={() => handleTestConfiguration(llmConfig)}
                        disabled={isTesting}
                        className="text-blue-600 hover:text-blue-800 p-1 disabled:text-gray-400"
                        title="测试连接"
                      >
                        {isTesting ? (
                          <LoadingOutlined className="w-4 h-4 animate-spin" />
                        ) : (
                          <ExperimentOutlined className="w-4 h-4" />
                        )}
                      </button>
                      
                      <button
                        onClick={() => handleEditConfiguration(llmConfig)}
                        className="text-gray-600 hover:text-gray-800 p-1"
                        title="编辑配置"
                      >
                        <EditOutlined className="w-4 h-4" />
                      </button>
                      
                      <button
                        onClick={() => handleDeleteConfiguration(llmConfig)}
                        className="text-red-600 hover:text-red-800 p-1"
                        title="删除配置"
                      >
                        <DeleteOutlined className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })
        )}
      </div>

      {/* 配置模态框 */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md mx-4">
            <h3 className="text-lg font-semibold text-gray-800 mb-4">
              {editingConfig ? '编辑配置' : '添加配置'}
            </h3>
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  模型类型 *
                </label>
                <select
                  value={formData.modelType}
                  onChange={(e) => handleModelTypeChange(e.target.value as 'OpenAI' | 'Doubao')}
                  className="w-full p-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-green-500 focus:border-transparent"
                >
                  <option value="OpenAI">OpenAI</option>
                  <option value="Doubao">豆包 (Doubao)</option>
                </select>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  模型名称 *
                </label>
                <input
                  type="text"
                  value={formData.modelName}
                  onChange={(e) => setFormData({ ...formData, modelName: e.target.value })}
                  placeholder={getDefaultModelName(formData.modelType)}
                  className="w-full p-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-green-500 focus:border-transparent"
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  API 端点 *
                </label>
                <input
                  type="url"
                  value={formData.endpoint}
                  onChange={(e) => setFormData({ ...formData, endpoint: e.target.value })}
                  placeholder={getDefaultEndpoint(formData.modelType)}
                  className="w-full p-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-green-500 focus:border-transparent"
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  API Key *
                </label>
                <input
                  type="password"
                  value={formData.apiKey}
                  onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
                  placeholder="请输入API Key"
                  className="w-full p-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-green-500 focus:border-transparent"
                />
              </div>
              
              <div className="flex items-center">
                <input
                  type="checkbox"
                  id="enabled"
                  checked={formData.enabled}
                  onChange={(e) => setFormData({ ...formData, enabled: e.target.checked })}
                  className="mr-2"
                />
                <label htmlFor="enabled" className="text-sm text-gray-700">
                  启用此配置
                </label>
              </div>
            </div>
            
            <div className="flex justify-end gap-3 mt-6">
              <button
                onClick={() => {
                  setIsModalOpen(false);
                  setEditingConfig(null);
                  resetForm();
                }}
                className="px-4 py-2 text-gray-600 hover:text-gray-800 transition-colors"
              >
                取消
              </button>
              <button
                onClick={handleSaveConfiguration}
                className="bg-green-600 hover:bg-green-700 text-white font-semibold py-2 px-4 rounded-lg transition-colors"
              >
                保存
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default LlmConfiguration;
