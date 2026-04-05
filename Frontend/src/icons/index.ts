import { addCollection } from "@iconify/vue";
import subsets from "virtual:iconify-subset";

let registered = false;

export const registerIcons = () => {
  if (registered) return;
  registered = true;
  for (const collection of Object.values(subsets)) {
    addCollection(collection as never);
  }
}
