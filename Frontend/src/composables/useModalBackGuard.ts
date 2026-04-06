import { useMediaQuery } from "@vueuse/core";
import { onBeforeUnmount, onMounted } from "vue";

export const useModalBackGuard = (onBack: () => void) => {
  const isMobile = useMediaQuery("(max-width: 767px)");

  onMounted(() => {
    if (!isMobile.value) return;
    history.pushState({ modalGuard: true }, "");

    window.addEventListener("popstate", handlePop);
  });

  onBeforeUnmount(() => {
    window.removeEventListener("popstate", handlePop);

    // If the modal is closed programmatically (not via back),
    // the synthetic entry is still in the stack — clean it up.
    if (history.state?.modalGuard) {
      history.back();
    }
  });

  const handlePop = () => {
    // The synthetic entry was popped — intercept and close
    window.removeEventListener("popstate", handlePop);
    onBack();
  };
};
