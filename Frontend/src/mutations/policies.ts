import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import {
  policyApi,
  type AddPolicyRuleRequest,
  type CreateDirectoryPolicyRequest,
  type UpdateDirectoryPolicyRequest,
  type UpdatePolicyRuleRequest,
} from "@/api/policy";
import { POLICY_QUERY_KEYS } from "@/queries/policies";

export const createPolicy = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: CreateDirectoryPolicyRequest) => policyApi.create(data),
    onSettled(_data, _error, data: CreateDirectoryPolicyRequest) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(data.directoryId),
      });
    },
  });
});

export const updatePolicy = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      policyId,
      directoryId: _directoryId,
      ...data
    }: UpdateDirectoryPolicyRequest & { policyId: string; directoryId: string }) =>
      policyApi.update(policyId, data),
    onSettled(_data, _error, { directoryId }: { directoryId: string }) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(directoryId),
      });
    },
  });
});

export const deletePolicy = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ policyId }: { policyId: string; directoryId: string }) =>
      policyApi.delete(policyId),
    onSettled(_data, _error, { directoryId }: { policyId: string; directoryId: string }) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(directoryId),
      });
    },
  });
});

export const addRule = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      policyId,
      directoryId: _directoryId,
      ...data
    }: AddPolicyRuleRequest & { policyId: string; directoryId: string }) =>
      policyApi.addRule(policyId, data),
    onSettled(_data, _error, { directoryId }: { directoryId: string }) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(directoryId),
      });
    },
  });
});

export const updateRule = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      ruleId,
      directoryId: _directoryId,
      ...data
    }: UpdatePolicyRuleRequest & { ruleId: string; directoryId: string }) =>
      policyApi.updateRule(ruleId, data),
    onSettled(_data, _error, { directoryId }: { directoryId: string }) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(directoryId),
      });
    },
  });
});

export const deleteRule = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ ruleId }: { ruleId: string; directoryId: string }) => policyApi.deleteRule(ruleId),
    onSettled(_data, _error, { directoryId }: { ruleId: string; directoryId: string }) {
      queryCache.invalidateQueries({
        key: POLICY_QUERY_KEYS.byDirectory(directoryId),
      });
    },
  });
});
