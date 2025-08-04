import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import type { ApiResponse } from '../types';

// 创建axios实例
const createHttpClient = (): AxiosInstance => {
  const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5094',
    timeout: 30000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  // 请求拦截器
  client.interceptors.request.use(
    (config) => {
      // 可以在这里添加认证token等
      return config;
    },
    (error) => {
      return Promise.reject(error);
    }
  );

  // 响应拦截器
  client.interceptors.response.use(
    (response: AxiosResponse) => {
      return response;
    },
    (error) => {
      // 统一错误处理
      if (error.response) {
        // 服务器返回错误状态码
        const { status, data } = error.response;
        
        switch (status) {
          case 400:
            console.error('请求参数错误:', data.message || '请求参数错误');
            break;
          case 401:
            console.error('未授权访问:', data.message || '未授权访问');
            break;
          case 403:
            console.error('禁止访问:', data.message || '禁止访问');
            break;
          case 404:
            console.error('资源不存在:', data.message || '资源不存在');
            break;
          case 429:
            console.error('请求频率过高:', data.message || '请求频率过高，请稍后重试');
            break;
          case 500:
            console.error('服务器内部错误:', data.message || '服务器内部错误');
            break;
          default:
            console.error('请求失败:', data.message || '请求失败');
        }
        
        return Promise.reject({
          success: false,
          message: data.message || '请求失败',
          code: status,
        });
      } else if (error.request) {
        // 网络错误
        console.error('网络连接失败:', error.message);
        return Promise.reject({
          success: false,
          message: '网络连接失败，请检查网络设置',
          code: 'NETWORK_ERROR',
        });
      } else {
        // 其他错误
        console.error('请求配置错误:', error.message);
        return Promise.reject({
          success: false,
          message: error.message || '请求失败',
          code: 'REQUEST_ERROR',
        });
      }
    }
  );

  return client;
};

// 导出HTTP客户端实例
export const httpClient = createHttpClient();

// 通用请求方法
export class HttpService {
  private client: AxiosInstance;

  constructor() {
    this.client = httpClient;
  }

  // GET请求
  async get<T>(
    url: string,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.get(url, config);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  // POST请求
  async post<T>(
    url: string,
    data?: any,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.post(url, data, config);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  // PUT请求
  async put<T>(
    url: string,
    data?: any,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.put(url, data, config);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  // DELETE请求
  async delete<T>(
    url: string,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    try {
      const response = await this.client.delete(url, config);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  // 文件上传
  async upload<T>(
    url: string,
    file: File,
    onProgress?: (progress: number) => void,
    config?: AxiosRequestConfig
  ): Promise<ApiResponse<T>> {
    const formData = new FormData();
    formData.append('file', file);

    try {
      const response = await this.client.post(url, formData, {
        ...config,
        headers: {
          'Content-Type': 'multipart/form-data',
          ...config?.headers,
        },
        onUploadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const progress = Math.round(
              (progressEvent.loaded * 100) / progressEvent.total
            );
            onProgress(progress);
          }
        },
      });
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  // 文件下载
  async download(
    url: string,
    filename?: string,
    onProgress?: (progress: number) => void,
    config?: AxiosRequestConfig
  ): Promise<void> {
    try {
      const response = await this.client.get(url, {
        ...config,
        responseType: 'blob',
        onDownloadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const progress = Math.round(
              (progressEvent.loaded * 100) / progressEvent.total
            );
            onProgress(progress);
          }
        },
      });

      // 创建下载链接
      const blob = new Blob([response.data]);
      const downloadUrl = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = downloadUrl;
      link.download = filename || 'download';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(downloadUrl);
    } catch (error) {
      throw error;
    }
  }
}

// 导出HTTP服务实例
export const httpService = new HttpService();