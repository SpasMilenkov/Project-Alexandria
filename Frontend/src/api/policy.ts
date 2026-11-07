import { api } from "./client";

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

// `source` is reserved for a future "apply already-computed enrichments" policy mode and is
// currently ignored by the trigger. The backend `AutoTagParameters` record still deserializes
// `source`, so we keep sending a fixed numeric value to stay wire-compatible.
export interface AutoTagParameters {
  source: 1;
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
      return await api.get<DirectoryPolicyDto>(`/policies/by-directory/${directoryId}`);
    } catch (error: any) {
      if (error?.status === 404) return null;
      throw error;
    }
  },

  create: (data: CreateDirectoryPolicyRequest): Promise<DirectoryPolicyDto> =>
    api.post<DirectoryPolicyDto>("/policies", data),

  update: (policyId: string, data: UpdateDirectoryPolicyRequest): Promise<DirectoryPolicyDto> =>
    api.patch<DirectoryPolicyDto>(`/policies/${policyId}`, data),

  delete: async (policyId: string): Promise<void> => {
    await api.delete(`/policies/${policyId}`);
  },

  addRule: (policyId: string, data: AddPolicyRuleRequest): Promise<PolicyRuleDto> =>
    api.post<PolicyRuleDto>(`/policies/${policyId}/rules`, data),

  updateRule: (ruleId: string, data: UpdatePolicyRuleRequest): Promise<PolicyRuleDto> =>
    api.patch<PolicyRuleDto>(`/policies/rules/${ruleId}`, data),

  deleteRule: async (ruleId: string): Promise<void> => {
    await api.delete(`/policies/rules/${ruleId}`);
  },
};
