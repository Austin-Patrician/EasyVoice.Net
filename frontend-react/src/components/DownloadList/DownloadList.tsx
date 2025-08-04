import React, { useState, useEffect } from 'react';
import { 
  Card, 
  List, 
  Button, 
  Progress, 
  Typography, 
  Space, 
  Tag, 
  Tooltip, 
  Modal, 
  message,
  Empty,
  Dropdown
} from 'antd';
import { 
  Download, 
  Play, 
  Pause, 
  Trash2, 
  FileText, 
  Clock, 
  CheckCircle, 
  XCircle,
  MoreVertical,
  RefreshCw
} from 'lucide-react';
import { useGenerationStore } from '../../stores/generationStore';
import { apiService } from '../../services/api';
import type { AudioItem, TaskStatusResponse } from '../../types';

const { Text, Paragraph } = Typography;

interface DownloadListProps {
  className?: string;
}

export const DownloadList: React.FC<DownloadListProps> = ({ className = '' }) => {
  const {
    audioList,
    updateAudioItem,
    removeAudioItem,
    clearAudioList,
    downloadState,
    setDownloadState
  } = useGenerationStore();

  const [playingId, setPlayingId] = useState<string | null>(null);

  const [pollingTasks, setPollingTasks] = useState<Set<string>>(new Set());
  const [audioElements, setAudioElements] = useState<Map<string, HTMLAudioElement>>(new Map());

  // 轮询任务状态
  useEffect(() => {
    const pollTaskStatus = async (taskId: string) => {
      try {
        const status = await apiService.getTaskStatus(taskId);
        const audioItem = audioList.find(item => item.taskId === taskId);
        
        if (!audioItem) return;

        if (status.status === 'completed' && status.result?.audioUrl) {
          const index = audioList.findIndex(audio => audio.taskId === taskId);
          if (index !== -1) updateAudioItem(index, {
            status: 'completed',
            audioUrl: status.result.audioUrl,
            srt: status.result.srtUrl,
            progress: 100
          });
          setPollingTasks(prev => {
            const newSet = new Set(prev);
            newSet.delete(taskId);
            return newSet;
          });
        } else if (status.status === 'failed') {
          const completedIndex = audioList.findIndex(audio => audio.taskId === taskId);
          if (completedIndex !== -1) updateAudioItem(completedIndex, {
            status: 'failed',
            error: status.error || '生成失败'
          });
          setPollingTasks(prev => {
            const newSet = new Set(prev);
            newSet.delete(taskId);
            return newSet;
          });
        } else if (status.status === 'processing') {
          const failedIndex = audioList.findIndex(audio => audio.taskId === taskId);
          if (failedIndex !== -1) updateAudioItem(failedIndex, {
            status: 'processing',
            progress: status.progress || audioItem.progress
          });
        }
      } catch (error) {
        console.error('Poll task status error:', error);
      }
    };

    const interval = setInterval(() => {
      pollingTasks.forEach(taskId => {
        pollTaskStatus(taskId);
      });
    }, 2000);

    return () => clearInterval(interval);
  }, [pollingTasks, audioList, updateAudioItem]);

  // 开始轮询新任务
  useEffect(() => {
    const processingTasks = audioList
      .filter(item => item.status === 'processing' || item.status === 'pending')
      .map(item => item.taskId);
    
    setPollingTasks(new Set(processingTasks));
  }, [audioList]);

  // 清理音频元素
  useEffect(() => {
    return () => {
      audioElements.forEach(audio => {
        audio.pause();
        audio.src = '';
      });
    };
  }, [audioElements]);

  const handleDownload = async (item: AudioItem) => {
    if (!item.audioUrl) {
      message.warning('音频文件尚未生成完成');
      return;
    }

    try {
      setDownloadState({ isDownloading: true, progress: 0 });
      
      const blob = await apiService.downloadFile(item.audioUrl, item.title || 'audio', (progress) => {
        setDownloadState({ isDownloading: true, progress });
      });
      
      // 创建下载链接
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${item.title || 'audio'}.${item.format || 'mp3'}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      
      setDownloadState({ isDownloading: false, progress: 0 });
      message.success('下载完成');
    } catch (error: any) {
      console.error('Download error:', error);
      setDownloadState({ isDownloading: false, progress: 0 });
      message.error(error.message || '下载失败');
    }
  };

  const handlePlay = async (item: AudioItem) => {
    if (!item.audioUrl) {
      message.warning('音频文件尚未生成完成');
      return;
    }

    try {
      // 停止其他正在播放的音频
      audioElements.forEach((audio, id) => {
        if (id !== item.taskId) {
          audio.pause();
        }
      });

      let audio = audioElements.get(item.taskId);
      
      if (!audio) {
        audio = new Audio();
        audio.src = item.audioUrl;
        audio.addEventListener('ended', () => {
          setPlayingId(null);
        });
        audio.addEventListener('error', () => {
          message.error('音频播放失败');
          setPlayingId(null);
        });
        
        setAudioElements(prev => new Map(prev.set(item.taskId, audio!)));
      }

      if (playingId === item.taskId) {
        audio.pause();
        setPlayingId(null);
      } else {
        await audio.play();
        setPlayingId(item.taskId);
      }
    } catch (error) {
      console.error('Play error:', error);
      message.error('音频播放失败');
    }
  };

  const handleRetry = async (item: AudioItem) => {
    try {
      const index = audioList.findIndex(audio => audio.taskId === item.taskId);
      if (index !== -1) updateAudioItem(index, {
        status: 'pending',
        error: undefined,
        progress: 0
      });
      
      // 重新生成
      const response = await apiService.generateVoice({
        text: item.text,
        voice: item.voice,
        gender: item.gender
      });
      
      const retryIndex = audioList.findIndex(audio => audio.taskId === item.taskId);
      if (retryIndex !== -1) updateAudioItem(retryIndex, {
        status: 'processing',
        taskId: response.id
      });
      
      setPollingTasks(prev => new Set(prev.add(response.id)));
      message.success('重新生成中...');
    } catch (error: any) {
      console.error('Retry error:', error);
      const errorIndex = audioList.findIndex(audio => audio.taskId === item.taskId);
      if (errorIndex !== -1) updateAudioItem(errorIndex, {
        status: 'failed',
        error: error.message || '重试失败'
      });
      message.error(error.message || '重试失败');
    }
  };

  const handleDelete = (item: AudioItem) => {
    Modal.confirm({
      title: '确认删除',
      content: `确定要删除"${item.title || '未命名'}"吗？`,
      okText: '删除',
      okType: 'danger',
      cancelText: '取消',
      onOk: () => {
        // 停止播放
        const audio = audioElements.get(item.taskId);
        if (audio) {
          audio.pause();
          audio.src = '';
        }
        
        if (playingId === item.taskId) {
          setPlayingId(null);
        }
        
        const index = audioList.findIndex(audio => audio.taskId === item.taskId);
        if (index !== -1) removeAudioItem(index);
        message.success('删除成功');
      }
    });
  };

  const handleClearAll = () => {
    if (audioList.length === 0) return;
    
    Modal.confirm({
      title: '确认清空',
      content: '确定要清空所有音频记录吗？此操作不可恢复。',
      okText: '清空',
      okType: 'danger',
      cancelText: '取消',
      onOk: () => {
        // 停止所有播放
        audioElements.forEach(audio => {
          audio.pause();
          audio.src = '';
        });
        setAudioElements(new Map());
        setPlayingId(null);
        
        clearAudioList();
        message.success('清空成功');
      }
    });
  };

  const getStatusTag = (status: AudioItem['status']) => {
    const statusConfig = {
      pending: { color: 'blue', icon: <Clock className="w-3 h-3" />, text: '等待中' },
      processing: { color: 'orange', icon: <RefreshCw className="w-3 h-3 animate-spin" />, text: '生成中' },
      completed: { color: 'green', icon: <CheckCircle className="w-3 h-3" />, text: '已完成' },
      failed: { color: 'red', icon: <XCircle className="w-3 h-3" />, text: '失败' }
    };
    
    const config = statusConfig[status];
    return (
      <Tag color={config.color} icon={config.icon}>
        {config.text}
      </Tag>
    );
  };

  const getDropdownItems = (item: AudioItem) => {
    const items = [];
    
    if (item.status === 'completed' && item.srt) {
      items.push({
        key: 'srt',
        icon: <FileText className="w-4 h-4" />,
        label: '下载字幕',
        onClick: () => handleDownload({ ...item, audio: item.srt || '', format: 'srt' })
      });
    }
    
    if (item.status === 'failed') {
      items.push({
        key: 'retry',
        icon: <RefreshCw className="w-4 h-4" />,
        label: '重试',
        onClick: () => handleRetry(item)
      });
    }
    
    items.push({
      key: 'delete',
      icon: <Trash2 className="w-4 h-4" />,
      label: '删除',
      danger: true,
      onClick: () => handleDelete(item)
    });
    
    return items;
  };

  if (audioList.length === 0) {
    return (
      <Card className={className}>
        <Empty
          description="暂无音频记录"
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </Card>
    );
  }

  return (
    <Card 
      className={className}
      title="音频列表"
      extra={
        <Button 
          type="text" 
          danger 
          size="small"
          onClick={handleClearAll}
          disabled={audioList.length === 0}
        >
          清空全部
        </Button>
      }
    >
      <List
        dataSource={audioList}
        renderItem={(item) => (
          <List.Item
            key={item.taskId}
            actions={[
              <Space key="actions">
                {item.status === 'completed' && (
                  <>
                    <Tooltip title="播放">
                      <Button
                        type="text"
                        size="small"
                        icon={playingId === item.taskId ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4" />}
                        onClick={() => handlePlay(item)}
                      />
                    </Tooltip>
                    
                    <Tooltip title="下载">
                      <Button
                        type="text"
                        size="small"
                        icon={<Download className="w-4 h-4" />}
                        onClick={() => handleDownload(item)}
                        loading={downloadState.isDownloading}
                      />
                    </Tooltip>
                  </>
                )}
                
                <Dropdown
                  menu={{ items: getDropdownItems(item) }}
                  trigger={['click']}
                >
                  <Button
                    type="text"
                    size="small"
                    icon={<MoreVertical className="w-4 h-4" />}
                  />
                </Dropdown>
              </Space>
            ]}
          >
            <List.Item.Meta
              title={
                <Space>
                  <Text strong>{item.title || '未命名'}</Text>
                  {getStatusTag(item.status)}
                </Space>
              }
              description={
                <div className="space-y-2">
                  <Paragraph 
                    ellipsis={{ rows: 2, expandable: true, symbol: '展开' }}
                    className="!mb-0 text-gray-600"
                  >
                    {item.text}
                  </Paragraph>
                  
                  {item.status === 'processing' && (
                    <Progress 
                      percent={item.progress} 
                      size="small" 
                      status="active"
                    />
                  )}
                  
                  {item.error && (
                    <Text type="danger" className="text-sm">
                      {item.error}
                    </Text>
                  )}
                  
                  <div className="flex items-center gap-4 text-xs text-gray-500">
                      <span>语音: {item.voice || '未知'}</span>
                      <span>性别: {item.gender || '未知'}</span>
                      <span>格式: MP3</span>
                      <span>创建时间: {new Date().toLocaleString()}</span>
                    </div>
                </div>
              }
            />
          </List.Item>
        )}
      />
    </Card>
  );
};