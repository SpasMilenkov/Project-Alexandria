import { defineStore, acceptHMRUpdate } from "pinia";
import {
  type AuditLogResult,
} from "@/api/activity";
import { computed, ref, type Ref } from "vue";

export const useActivityStore = defineStore("activity", () => {
  const activity: Ref<AuditLogResult[]> = ref([]);
  const page = ref(1)
  const pageSize = ref (10)
  const totalCount = ref(0)
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const getActivity = computed(() => activity.value);
  const getPage = computed(() => page.value)
  const getPageSize = computed(() => pageSize.value)
  const getTotalCount = computed(() => totalCount.value)
  
  return {
    // State
    activity,
    page,
    pageSize,
    totalCount,
    isLoading,
    error,
    // Getters
    getActivity,
    getPage,
    getPageSize,
    getTotalCount,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useActivityStore, import.meta.hot));
}
