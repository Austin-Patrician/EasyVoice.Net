import { message } from 'antd';
import { handleError, showSuccess, showInfo } from './errorHandler';

export interface RetryOptions {
  maxAttempts: number;
  delay: number;
  backoff?: boolean;
  onRetry?: (attempt: number, error: Error) => void;
}

export interface LoadingState {
  isLoading: boolean;
  message?: string;
  progress?: number;
}

export class UserExperienceManager {
  private static instance: UserExperienceManager;
  private loadingStates: Map<string, LoadingState> = new Map();
  private retryAttempts: Map<string, number> = new Map();

  private constructor() {}

  public static getInstance(): UserExperienceManager {
    if (!UserExperienceManager.instance) {
      UserExperienceManager.instance = new UserExperienceManager();
    }
    return UserExperienceManager.instance;
  }

  // 加载状态管理
  public setLoading(key: string, isLoading: boolean, message?: string, progress?: number): void {
    if (isLoading) {
      this.loadingStates.set(key, { isLoading, message, progress });
    } else {
      this.loadingStates.delete(key);
    }
  }

  public getLoadingState(key: string): LoadingState | undefined {
    return this.loadingStates.get(key);
  }

  public isLoading(key: string): boolean {
    return this.loadingStates.get(key)?.isLoading || false;
  }

  public getAllLoadingStates(): Map<string, LoadingState> {
    return new Map(this.loadingStates);
  }

  // 重试机制
  public async withRetry<T>(
    operation: () => Promise<T>,
    options: RetryOptions,
    operationKey?: string
  ): Promise<T> {
    const { maxAttempts, delay, backoff = true, onRetry } = options;
    let lastError: Error;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        if (operationKey) {
          this.retryAttempts.set(operationKey, attempt);
        }

        const result = await operation();
        
        // 成功后清除重试计数
        if (operationKey) {
          this.retryAttempts.delete(operationKey);
        }
        
        return result;
      } catch (error) {
        lastError = error instanceof Error ? error : new Error(String(error));
        
        if (attempt === maxAttempts) {
          // 最后一次尝试失败
          if (operationKey) {
            this.retryAttempts.delete(operationKey);
          }
          throw lastError;
        }

        // 调用重试回调
        if (onRetry) {
          onRetry(attempt, lastError);
        }

        // 计算延迟时间
        const currentDelay = backoff ? delay * Math.pow(2, attempt - 1) : delay;
        await this.sleep(currentDelay);
      }
    }

    throw lastError!;
  }

  public getRetryAttempt(operationKey: string): number {
    return this.retryAttempts.get(operationKey) || 0;
  }

  // 工具方法
  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  // 用户友好的操作包装器
  public async performOperation<T>(
    operation: () => Promise<T>,
    options: {
      loadingKey?: string;
      loadingMessage?: string;
      successMessage?: string;
      errorContext?: string;
      retry?: RetryOptions;
      showProgress?: boolean;
    } = {}
  ): Promise<T> {
    const {
      loadingKey,
      loadingMessage = '处理中...',
      successMessage,
      errorContext,
      retry,
      showProgress = false
    } = options;

    try {
      // 设置加载状态
      if (loadingKey) {
        this.setLoading(loadingKey, true, loadingMessage);
      }

      let result: T;

      if (retry) {
        // 使用重试机制
        result = await this.withRetry(
          operation,
          {
            ...retry,
            onRetry: (attempt, error) => {
              if (loadingKey && showProgress) {
                this.setLoading(
                  loadingKey,
                  true,
                  `${loadingMessage} (重试 ${attempt}/${retry.maxAttempts})`,
                  (attempt / retry.maxAttempts) * 100
                );
              }
              if (retry.onRetry) {
                retry.onRetry(attempt, error);
              }
            }
          },
          loadingKey
        );
      } else {
        // 直接执行
        result = await operation();
      }

      // 显示成功消息
      if (successMessage) {
        showSuccess(successMessage);
      }

      return result;
    } catch (error) {
      // 处理错误
      handleError(error instanceof Error ? error : new Error(String(error)), errorContext);
      throw error;
    } finally {
      // 清除加载状态
      if (loadingKey) {
        this.setLoading(loadingKey, false);
      }
    }
  }

  // 连接状态管理
  public async waitForConnection(
    checkConnection: () => boolean,
    options: {
      timeout?: number;
      interval?: number;
      onProgress?: (elapsed: number) => void;
    } = {}
  ): Promise<void> {
    const { timeout = 30000, interval = 1000, onProgress } = options;
    const startTime = Date.now();

    return new Promise((resolve, reject) => {
      const checkInterval = setInterval(() => {
        const elapsed = Date.now() - startTime;
        
        if (onProgress) {
          onProgress(elapsed);
        }

        if (checkConnection()) {
          clearInterval(checkInterval);
          resolve();
        } else if (elapsed >= timeout) {
          clearInterval(checkInterval);
          reject(new Error('连接超时'));
        }
      }, interval);
    });
  }

  // 防抖函数
  public debounce<T extends (...args: any[]) => any>(
    func: T,
    delay: number
  ): (...args: Parameters<T>) => void {
    let timeoutId: NodeJS.Timeout;
    
    return (...args: Parameters<T>) => {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => func(...args), delay);
    };
  }

  // 节流函数
  public throttle<T extends (...args: any[]) => any>(
    func: T,
    delay: number
  ): (...args: Parameters<T>) => void {
    let lastCall = 0;
    
    return (...args: Parameters<T>) => {
      const now = Date.now();
      if (now - lastCall >= delay) {
        lastCall = now;
        func(...args);
      }
    };
  }

  // 用户引导和提示
  public showGuidance(steps: Array<{ title: string; content: string; duration?: number }>): void {
    let currentStep = 0;
    
    const showNextStep = () => {
      if (currentStep < steps.length) {
        const step = steps[currentStep];
        showInfo(`${step.title}: ${step.content}`, step.duration || 4);
        currentStep++;
        
        if (currentStep < steps.length) {
          setTimeout(showNextStep, (step.duration || 4) * 1000);
        }
      }
    };
    
    showNextStep();
  }

  // 性能监控
  public measurePerformance<T>(
    operation: () => T | Promise<T>,
    operationName: string
  ): Promise<{ result: T; duration: number }> {
    const startTime = performance.now();
    
    const finish = (result: T) => {
      const duration = performance.now() - startTime;
      console.log(`[Performance] ${operationName}: ${duration.toFixed(2)}ms`);
      return { result, duration };
    };

    try {
      const result = operation();
      
      if (result instanceof Promise) {
        return result.then(finish);
      } else {
        return Promise.resolve(finish(result));
      }
    } catch (error) {
      const duration = performance.now() - startTime;
      console.error(`[Performance] ${operationName} failed after ${duration.toFixed(2)}ms:`, error);
      throw error;
    }
  }

  // 清理资源
  public cleanup(): void {
    this.loadingStates.clear();
    this.retryAttempts.clear();
  }
}

// 导出单例实例
export const uxManager = UserExperienceManager.getInstance();

// 便捷函数
export const withRetry = <T>(
  operation: () => Promise<T>,
  options: RetryOptions,
  operationKey?: string
) => {
  return uxManager.withRetry(operation, options, operationKey);
};

export const performOperation = <T>(
  operation: () => Promise<T>,
  options?: Parameters<typeof uxManager.performOperation>[1]
) => {
  return uxManager.performOperation(operation, options);
};

export const setLoading = (key: string, isLoading: boolean, message?: string, progress?: number) => {
  uxManager.setLoading(key, isLoading, message, progress);
};

export const isLoading = (key: string) => {
  return uxManager.isLoading(key);
};

export const debounce = <T extends (...args: any[]) => any>(func: T, delay: number) => {
  return uxManager.debounce(func, delay);
};

export const throttle = <T extends (...args: any[]) => any>(func: T, delay: number) => {
  return uxManager.throttle(func, delay);
};

export const measurePerformance = <T>(
  operation: () => T | Promise<T>,
  operationName: string
) => {
  return uxManager.measurePerformance(operation, operationName);
};