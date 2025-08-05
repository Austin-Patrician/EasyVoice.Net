# TODO:

- [x] 1: 修改generationStore.ts，将存储名称从'easy-voice-app-store'改为'audio-config-storage' (priority: High)
- [x] 2: 更新types/index.ts，从AudioConfig接口中移除llmSettings字段，只保留llmConfiguration (priority: High)
- [x] 3: 修改audioConfigStore.ts，移除所有llmSettings相关的代码和方法 (priority: High)
- [x] 4: 修改SettingsModal组件，移除handleSave中的required验证，允许保存空配置 (priority: Medium)
- [x] 5: 修改GeneratePage组件，更新LLM配置验证逻辑，只在生成语音时验证 (priority: Medium)
- [x] 6: 更新constants/index.ts中的默认配置，移除llmSettings相关配置 (priority: Medium)
- [x] 7: 测试修改后的功能，确保配置保存和语音生成正常工作 (priority: Low)
