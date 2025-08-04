import { httpService } from '../lib/http';
import type {
  GenerateRequest,
  GenerateResponse,
  VoiceListResponse,
  TaskStatusResponse,
  Voice,
  Task,
} from '../types';
import { API_ENDPOINTS, POLLING_INTERVAL } from '../constants';

/**
 * API服务类
 * 封装所有与后端交互的接口
 */
export class ApiService {
  /**
   * 获取语音列表
   */
  async getVoices(): Promise<Voice[]> {
    try {
      const response = await httpService.get<Voice[]>(API_ENDPOINTS.VOICES);
      if (response.success) {
        return response.data;
      }
      throw new Error('获取语音列表失败');
    } catch (error) {
      console.error('获取语音列表失败:', error);
      throw error;
    }
  }

  /**
   * 生成语音
   */
  async generateAudio(request: GenerateRequest): Promise<GenerateResponse['data']> {
    try {
      const response = await httpService.post<GenerateResponse['data']>(
        API_ENDPOINTS.GENERATE,
        request
      );
      if (response.success) {
        return response.data;
      }
      throw new Error('语音生成失败');
    } catch (error) {
      console.error('语音生成失败:', error);
      throw error;
    }
  }



  /**
   * 获取任务状态
   */
  async getTaskStatus(taskId: string): Promise<TaskStatusResponse['data']> {
    try {
      const response = await httpService.get<TaskStatusResponse['data']>(
        `${API_ENDPOINTS.TASK_STATUS}/${taskId}`
      );
      if (response.success) {
        return response.data;
      }
      throw new Error('获取任务状态失败');
    } catch (error) {
      console.error('获取任务状态失败:', error);
      throw error;
    }
  }

  /**
   * 轮询任务状态直到完成
   */
  async pollTaskStatus(
    taskId: string,
    onProgress?: (progress: number) => void,
    maxAttempts: number = 300 // 最多轮询5分钟
  ): Promise<TaskStatusResponse['data']> {
    let attempts = 0;
    
    return new Promise((resolve, reject) => {
      const poll = async () => {
        try {
          attempts++;
          
          if (attempts > maxAttempts) {
            reject(new Error('任务超时'));
            return;
          }

          const status = await this.getTaskStatus(taskId);
          
          // 更新进度
          if (onProgress) {
            onProgress(status.progress);
          }

          // 检查任务状态
          if (status.status === 'completed') {
            resolve(status);
          } else if (status.status === 'failed') {
            reject(new Error('任务执行失败'));
          } else {
            // 继续轮询
            setTimeout(poll, POLLING_INTERVAL);
          }
        } catch (error) {
          reject(error);
        }
      };

      poll();
    });
  }

  /**
   * 生成语音（简化版本，返回完整响应）
   */
  async generateVoice(
    request: GenerateRequest,
    signal?: AbortSignal
  ): Promise<{ audio: string; file: string; srt?: string; size?: number; id: string; }> {
    try {
      const result = await this.generateAudio(request);
      return {
        audio: result.audio,
        file: result.file,
        srt: result.srt,
        size: result.size,
        id: result.id,
      };
    } catch (error) {
      console.error('语音生成失败:', error);
      throw error;
    }
  }

  /**
   * 下载文件
   */
  async downloadFile(
    fileUrl: string,
    filename: string,
    onProgress?: (progress: number) => void
  ): Promise<Blob> {
    try {
      const response = await httpService.get(fileUrl, {
        responseType: 'blob',
        onDownloadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
            onProgress(progress);
          }
        },
      });
      
      if (response instanceof Blob) {
        return response;
      }
      
      throw new Error('下载文件失败');
    } catch (error) {
      console.error('文件下载失败:', error);
      throw error;
    }
  }

  /**
   * 获取音频文件的Blob URL
   */
  async getAudioBlob(audioUrl: string): Promise<string> {
    try {
      const response = await httpService.get(audioUrl, {
        responseType: 'blob',
      });
      
      if (response instanceof Blob) {
        return URL.createObjectURL(response);
      }
      
      throw new Error('获取音频文件失败');
    } catch (error) {
      console.error('获取音频文件失败:', error);
      throw error;
    }
  }

  /**
   * 预览语音
   */
  async previewVoice(
    text: string,
    voice: string,
    rate?: number,
    pitch?: number
  ): Promise<string> {
    try {
      const request: GenerateRequest = {
        text: text.slice(0, 100), // 限制预览文本长度
        voice,
        rate: rate?.toString(),
        pitch: pitch?.toString(),
      };

      const result = await this.generateAudio(request);
      return result.audio;
    } catch (error) {
      console.error('语音预览失败:', error);
      throw error;
    }
  }

  /**
   * AI语音生成
   */
  async generateAIVoice(
    text: string,
    openaiConfig: {
      baseUrl: string;
      apiKey: string;
      model: string;
    }
  ): Promise<GenerateResponse['data']> {
    try {
      const request: GenerateRequest = {
        text,
        useLLM: true,
        openaiBaseUrl: openaiConfig.baseUrl,
        openaiKey: openaiConfig.apiKey,
        openaiModel: openaiConfig.model,
      };

      return await this.generateAudio(request);
    } catch (error) {
      console.error('AI语音生成失败:', error);
      throw error;
    }
  }

  /**
   * OpenAI TTS语音生成
   */
  async generateOpenAITTS(
    text: string,
    config: {
      baseUrl: string;
      apiKey: string;
      model: string;
      voice: string;
      speed?: number;
      pitch?: number;
      volume?: number;
    }
  ): Promise<GenerateResponse['data']> {
    try {
      const response = await httpService.post<GenerateResponse['data']>(
        API_ENDPOINTS.OPENAI_TTS,
        {
          text,
          ...config,
        }
      );
      if (response.success) {
        return response.data;
      }
      throw new Error('OpenAI TTS语音生成失败');
    } catch (error) {
      console.error('OpenAI TTS语音生成失败:', error);
      throw error;
    }
  }

  /**
   * 豆包TTS语音生成
   */
  async generateDoubaoTTS(
    text: string,
    config: {
      appId: string;
      accessToken: string;
      cluster: string;
      endpoint: string;
      audioEncoding: string;
      speed?: number;
      pitch?: number;
      volume?: number;
    }
  ): Promise<GenerateResponse['data']> {
    try {
      const response = await httpService.post<GenerateResponse['data']>(
        API_ENDPOINTS.DOUBAO_TTS,
        {
          text,
          ...config,
        }
      );
      if (response.success) {
        return response.data;
      }
      throw new Error('豆包TTS语音生成失败');
    } catch (error) {
      console.error('豆包TTS语音生成失败:', error);
      throw error;
    }
  }

  /**
   * 根据语音模式生成语音
   */
  async generateVoiceByMode(
    text: string,
    voiceMode: 'edge' | 'llm',
    llmProvider: 'openai' | 'doubao',
    edgeConfig?: {
      voice: string;
      rate?: number;
      pitch?: number;
      volume?: number;
    },
    llmConfiguration?: {
      openai: {
        baseUrl: string;
        apiKey: string;
        model: string;
        voice: string;
        speed?: number;
        pitch?: number;
        volume?: number;
      };
      doubao: {
        appId: string;
        accessToken: string;
        cluster: string;
        endpoint: string;
        audioEncoding: string;
        speed?: number;
        pitch?: number;
        volume?: number;
      };
    }
  ): Promise<GenerateResponse['data']> {
    try {
      if (voiceMode === 'edge') {
        // Edge语音模式，调用原有的generate接口
        const request: GenerateRequest = {
          text,
          voice: edgeConfig?.voice || '',
          rate: edgeConfig?.rate?.toString(),
          pitch: edgeConfig?.pitch?.toString(),
        };
        return await this.generateAudio(request);
      } else {
        // LLM语音模式，根据提供商调用不同接口
        if (!llmConfiguration) {
          throw new Error('LLM配置不能为空');
        }
        
        if (llmProvider === 'openai') {
          return await this.generateOpenAITTS(text, llmConfiguration.openai);
        } else {
          return await this.generateDoubaoTTS(text, llmConfiguration.doubao);
        }
      }
    } catch (error) {
      console.error('语音生成失败:', error);
      throw error;
    }
  }

  /**
   * 批量生成语音
   */
  async batchGenerateAudio(
    requests: GenerateRequest[],
    onProgress?: (completed: number, total: number) => void
  ): Promise<GenerateResponse['data'][]> {
    const results: GenerateResponse['data'][] = [];
    
    for (let i = 0; i < requests.length; i++) {
      try {
        const result = await this.generateAudio(requests[i]);
        results.push(result);
        
        if (onProgress) {
          onProgress(i + 1, requests.length);
        }
      } catch (error) {
        console.error(`批量生成第${i + 1}个语音失败:`, error);
        throw error;
      }
    }
    
    return results;
  }

  /**
   * 获取SRT字幕文件
   */
  async getSrtFile(srtUrl: string): Promise<string> {
    try {
      const response = await httpService.get(srtUrl, {
        responseType: 'text',
      });
      
      if (typeof response === 'string') {
        return response;
      }
      
      throw new Error('获取SRT文件失败');
    } catch (error) {
      console.error('获取SRT文件失败:', error);
      throw error;
    }
  }
}

// 创建并导出API服务实例
export const apiService = new ApiService();

// 导出便捷方法
export const {
  getVoices,
  generateAudio,
  generateVoice,
  getTaskStatus,
  pollTaskStatus,
  downloadFile,
  getAudioBlob,
  previewVoice,
  generateAIVoice,
  generateOpenAITTS,
  generateDoubaoTTS,
  generateVoiceByMode,
  batchGenerateAudio,
  getSrtFile,
} = apiService;

// 导出默认实例
export default apiService;