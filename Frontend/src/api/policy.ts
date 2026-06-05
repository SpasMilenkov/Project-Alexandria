import { apiClient } from "./client";

// Enums mirroring the backend

export enum PolicyActionType {
  Transcode = 0,
  AutoTag = 1,
  Backup = 2,
}

export enum PolicyTriggerType {
  AnyFile = 0,
  FileGroup = 1,
}

export enum VideoRung {
  P360 = 0,
  P480 = 1,
  P720 = 2,
  P1080 = 3,
  P1440 = 4,
  P2160 = 5,
}

export enum AudioRung {
  Kbps96 = 0,
  Kbps128 = 1,
  Kbps192 = 2,
  Kbps256 = 3,
  Kbps320 = 4,
}

export enum BackupFrequency {
  Daily = 0,
  Every3Days = 1,
  Weekly = 2,
  Monthly = 3,
  Every3Months = 4,
}

export enum TagSource {
  FileName = 0,
  FileMetadata = 1,
}

// Parameter shapes per action type

export interface TranscodeParameters {
  videoRungs: VideoRung[];
  audioRungs: AudioRung[];
  generateThumbnail: boolean;
}

export interface BackupParameters {
  destinationPath: string;
  frequency: BackupFrequency;
}

export interface AutoTagParameters {
  source: TagSource;
}

export type PolicyRuleParameters = TranscodeParameters | BackupParameters | AutoTagParameters;

// DTOs

export interface PolicyRuleDto {
  id: string;
  policyId: string;
  actionType: PolicyActionType;
  triggerType: PolicyTriggerType;
  triggerValue: string;
  priority: number;
  applyOnNewVersion: boolean;
  parameters: PolicyRuleParameters;
  createdAt: string;
  updatedAt: string;
}

export interface DirectoryPolicyDto {
  id: string;
  directoryId: string;
  inheritedByChildren: boolean;
  rules: PolicyRuleDto[];
  createdAt: string;
  updatedAt: string;
}

// Request types

export interface CreateDirectoryPolicyRequest {
  directoryId: string;
  inheritedByChildren: boolean;
}

export interface UpdateDirectoryPolicyRequest {
  inheritedByChildren: boolean;
}

export interface AddPolicyRuleRequest {
  actionType: PolicyActionType;
  triggerType: PolicyTriggerType;
  triggerValue: string;
  priority: number;
  applyOnNewVersion: boolean;
  parameters: PolicyRuleParameters;
}

export interface UpdatePolicyRuleRequest {
  triggerType: PolicyTriggerType;
  triggerValue: string;
  priority: number;
  applyOnNewVersion: boolean;
  parameters: PolicyRuleParameters;
}

export const policyApi = {
  getByDirectory: async (directoryId: string): Promise<DirectoryPolicyDto | null> => {
    try {
      const response = await apiClient.get<DirectoryPolicyDto>(
        `/policies/by-directory/${directoryId}`,
      );
      return response.data;
    } catch (error: any) {
      if (error?.response?.status === 404) return null;
      throw error;
    }
  },

  create: async (data: CreateDirectoryPolicyRequest): Promise<DirectoryPolicyDto> => {
    const response = await apiClient.post<DirectoryPolicyDto>("/policies", data);
    return response.data;
  },

  update: async (
    policyId: string,
    data: UpdateDirectoryPolicyRequest,
  ): Promise<DirectoryPolicyDto> => {
    const response = await apiClient.patch<DirectoryPolicyDto>(`/policies/${policyId}`, data);
    return response.data;
  },

  delete: async (policyId: string): Promise<void> => {
    await apiClient.delete(`/policies/${policyId}`);
  },

  addRule: async (policyId: string, data: AddPolicyRuleRequest): Promise<PolicyRuleDto> => {
    const response = await apiClient.post<PolicyRuleDto>(`/policies/${policyId}/rules`, data);
    return response.data;
  },

  updateRule: async (ruleId: string, data: UpdatePolicyRuleRequest): Promise<PolicyRuleDto> => {
    const response = await apiClient.patch<PolicyRuleDto>(`/policies/rules/${ruleId}`, data);
    return response.data;
  },

  deleteRule: async (ruleId: string): Promise<void> => {
    await apiClient.delete(`/policies/rules/${ruleId}`);
  },
};
