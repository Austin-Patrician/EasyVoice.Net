import React from 'react';
import { notification, message } from 'antd';
import { REALTIME_ERROR_MESSAGES } from '../constants';

export interface ErrorInfo {
  code?: string;
  message: string;
  details?: any;
  timestamp: number;
  context?: string;
}

export class ErrorHandler {
  private static instance: ErrorHandler;
  private errorLog: ErrorInfo[] = [];
  private maxLogSize = 100;

  private constructor() {}

  public static getInstance(): ErrorHandler {
    if (!ErrorHandler.instance) {
      ErrorHandler.instance = new ErrorHandler();
    }
    return ErrorHandler.instance;
  }

  public handleError(error: Error | string, context?: string, showNotification = true): ErrorInfo {
    const errorInfo: ErrorInfo = {
      message: typeof error === 'string' ? error : error.message,
      timestamp: Date.now(),
      context,
      details: typeof error === 'object' ? error : undefined
    };

    // 添加到错误日志
    this.addToLog(errorInfo);

    // 显示用户友好的错误消息
    if (showNotification) {
      this.showUserFriendlyError(errorInfo);
    }

    // 控制台输出详细错误信息
    console.error(`[${context || 'Unknown'}] Error:`, error);

    return errorInfo;
  }

  private addToLog(errorInfo: ErrorInfo): void {
    this.errorLog.unshift(errorInfo);
    if (this.errorLog.length > this.maxLogSize) {
      this.errorLog = this.errorLog.slice(0, this.maxLogSize);
    }
  }

  private showUserFriendlyError(errorInfo: ErrorInfo): void {
    const userMessage = this.getUserFriendlyMessage(errorInfo.message);
    const isNetworkError = this.isNetworkError(errorInfo.message);
    const isPermissionError = this.isPermissionError(errorInfo.message);

    if (isNetworkError) {
      notification.error({
        message: '网络连接错误',
        description: userMessage,
        duration: 5,
        placement: 'topRight'
      });
    } else if (isPermissionError) {
      notification.warning({
        message: '权限请求',
        description: userMessage,
        duration: 8,
        placement: 'topRight',
        btn: React.createElement('button', {
          onClick: () => window.location.reload(),
          style: {
            background: '#1890ff',
            color: 'white',
            border: 'none',
            padding: '4px 12px',
            borderRadius: '4px',
            cursor: 'pointer'
          }
        }, '重新加载')
      });
    } else {
      message.error(userMessage, 4);
    }
  }

  private getUserFriendlyMessage(errorMessage: string): string {
    // 网络相关错误
    if (errorMessage.includes('fetch') || errorMessage.includes('network') || errorMessage.includes('NetworkError')) {
      return '网络连接失败，请检查您的网络连接后重试';
    }

    // WebSocket相关错误
    if (errorMessage.includes('WebSocket') || errorMessage.includes('websocket')) {
      return '实时连接失败，请稍后重试';
    }

    // 音频权限相关错误
    if (errorMessage.includes('permission') || errorMessage.includes('Permission') || 
        errorMessage.includes('NotAllowedError') || errorMessage.includes('麦克风权限')) {
      return '需要麦克风权限才能进行语音对话，请在浏览器设置中允许访问麦克风';
    }

    // 音频设备相关错误
    if (errorMessage.includes('audio') || errorMessage.includes('microphone') || 
        errorMessage.includes('NotFoundError') || errorMessage.includes('音频设备')) {
      return '未找到可用的音频设备，请检查您的麦克风连接';
    }

    // 会话相关错误
    if (errorMessage.includes('session') || errorMessage.includes('会话')) {
      return '语音会话出现问题，请重新开始对话';
    }

    // 连接超时
    if (errorMessage.includes('timeout') || errorMessage.includes('超时')) {
      return '连接超时，请检查网络连接后重试';
    }

    // 服务器错误
    if (errorMessage.includes('500') || errorMessage.includes('502') || errorMessage.includes('503')) {
      return '服务暂时不可用，请稍后重试';
    }

    // 认证错误
    if (errorMessage.includes('401') || errorMessage.includes('403') || errorMessage.includes('Unauthorized')) {
      return '认证失败，请重新登录';
    }

    // 使用预定义的错误消息
    for (const [key, value] of Object.entries(REALTIME_ERROR_MESSAGES)) {
      if (errorMessage.includes(value) || errorMessage.includes(key.toLowerCase())) {
        return value;
      }
    }

    // 默认错误消息
    return errorMessage || '发生未知错误，请稍后重试';
  }

  private isNetworkError(errorMessage: string): boolean {
    const networkKeywords = ['fetch', 'network', 'NetworkError', 'timeout', '超时', 'connection', 'ECONNREFUSED'];
    return networkKeywords.some(keyword => errorMessage.toLowerCase().includes(keyword.toLowerCase()));
  }

  private isPermissionError(errorMessage: string): boolean {
    const permissionKeywords = ['permission', 'Permission', 'NotAllowedError', '权限', 'denied'];
    return permissionKeywords.some(keyword => errorMessage.toLowerCase().includes(keyword.toLowerCase()));
  }

  public getErrorLog(): ErrorInfo[] {
    return [...this.errorLog];
  }

  public clearErrorLog(): void {
    this.errorLog = [];
  }

  public getRecentErrors(count = 5): ErrorInfo[] {
    return this.errorLog.slice(0, count);
  }

  public hasRecentErrors(timeWindow = 60000): boolean {
    const now = Date.now();
    return this.errorLog.some(error => now - error.timestamp < timeWindow);
  }

  public getErrorStats(): { total: number; recent: number; byContext: Record<string, number> } {
    const now = Date.now();
    const recentWindow = 300000; // 5分钟
    const byContext: Record<string, number> = {};

    let recentCount = 0;
    this.errorLog.forEach(error => {
      if (now - error.timestamp < recentWindow) {
        recentCount++;
      }
      const context = error.context || 'Unknown';
      byContext[context] = (byContext[context] || 0) + 1;
    });

    return {
      total: this.errorLog.length,
      recent: recentCount,
      byContext
    };
  }
}

// 导出单例实例
export const errorHandler = ErrorHandler.getInstance();

// 便捷函数
export const handleError = (error: Error | string, context?: string, showNotification = true) => {
  return errorHandler.handleError(error, context, showNotification);
};

export const showSuccess = (msg: string, duration = 3) => {
  message.success(msg, duration);
};

export const showWarning = (msg: string, duration = 4) => {
  message.warning(msg, duration);
};

export const showInfo = (msg: string, duration = 3) => {
  message.info(msg, duration);
};

// 全局错误处理
if (typeof window !== 'undefined') {
  // 捕获未处理的Promise拒绝
  window.addEventListener('unhandledrejection', (event) => {
    handleError(event.reason, 'UnhandledPromiseRejection', false);
  });

  // 捕获全局JavaScript错误
  window.addEventListener('error', (event) => {
    handleError(event.error || event.message, 'GlobalError', false);
  });
}