import { defineStore, acceptHMRUpdate } from "pinia";
import {
  type AuditLogQuery,
  type AuditLogResult,
  activityApi,
} from "@/api/activity";
import { computed, ref, type Ref } from "vue";
import { handleError } from "@/utils/helper";

export const useActivityStore = defineStore("activity", () => {
  const activity: Ref<AuditLogResult[]> = ref([]);
  const page = ref(1)
  const pageSize = ref (50)
  const totalCount = ref(0)
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const getActivity = computed(() => activity.value);
  const getPage = computed(() => page.value)
  const getPageSize = computed(() => pageSize.value)
  const getTotalCount = computed(() => totalCount.value)
  const fetchActivity = async (query: AuditLogQuery) => {
    isLoading.value = true;
    console.log(' are wer here even')
    try {
        console.log('we are fetching here')
      const result = await activityApi.getUserActivity(query);
      console.log('store values:')
      console.log(result.items)
      activity.value = [...result.items]
      page.value = result.currentPage
      pageSize.value = result.pageSize
      totalCount.value = result.totalCount

      return { success: true, data: result };
    } catch (err: unknown) {
      const message = handleError(err, "Failed to generate signed URL");
      error.value = message;
      return { success: false, error: message };
    } finally {
      isLoading.value = false;
    }
  };

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
    // Actions
    fetchActivity,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useActivityStore, import.meta.hot));
}
