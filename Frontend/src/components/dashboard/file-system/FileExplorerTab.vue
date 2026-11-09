<template>
  <div
    class="@container/explorer flex h-full min-h-0 w-full min-w-0 flex-1 flex-col gap-0 p-0 md:gap-3 md:p-3"
    @click="handleContainerClick"
  >
    <!-- Toolbar -->
    <div
      class="flex w-full shrink-0 flex-col overflow-hidden rounded-none border border-x-0 border-gray-200/70 frosted-glass glass-surface-strong md:rounded-xl md:border-x dark:border-gray-700/70"
    >
      <!-- Desktop toolbar -->
      <div class="hidden @3xl/explorer:flex h-full w-full items-center gap-2 px-3 py-2">
        <!-- Upload split button -->
        <div class="flex shrink-0 overflow-hidden rounded-lg">
          <UButton
            color="primary"
            size="sm"
            class="rounded-none font-semibold"
            @click="handleFileUpload(selectedUploadType.label as 'File' | 'Directory' | 'Archive')"
          >
            <Icon :icon="selectedUploadType.icon" class="w-4 h-4 mr-2" />
            Upload {{ selectedUploadType.label }}
          </UButton>
          <UDropdownMenu :items="uploadOptions" :ui="{ content: 'w-48' }">
            <UButton
              color="primary"
              size="sm"
              class="w-8 justify-center rounded-none border-l border-white/25 px-0 dark:border-black/25"
              aria-label="Upload options"
            >
              <Icon icon="mdi:chevron-down" class="w-4 h-4" />
            </UButton>
          </UDropdownMenu>
        </div>

        <!-- Narrow strips drop the label so the listing cluster never gets squeezed. -->
        <UButton
          variant="outline"
          color="neutral"
          size="sm"
          class="shrink-0 gap-1 rounded-lg"
          aria-label="New Folder"
          title="New Folder"
          @click="createNewDirectory"
        >
          <Icon icon="mdi:folder-plus" class="w-4 h-4" />
          <span class="hidden @5xl/explorer:inline">New Folder</span>
        </UButton>

        <div class="flex-1" />

        <!-- Throbber + last-refreshed label -->
        <div class="flex items-center gap-1.5 text-xs select-none" aria-live="polite">
          <Transition name="throbber">
            <BlocksSpinner
              v-if="isBackgroundLoading"
              :size="14"
              aria-label="Refreshing"
              class="opacity-40"
            />
          </Transition>
          <Transition name="fade-status">
            <span
              v-if="lastRefreshedLabel && !isBackgroundLoading"
              class="hidden tabular-nums text-gray-600 @5xl/explorer:inline dark:text-gray-400"
              :title="lastRefreshedAt?.toLocaleTimeString()"
            >
              {{ lastRefreshedLabel }}
            </span>
          </Transition>
        </div>

        <!-- Order: sort field + direction -->
        <div :class="pillClass">
          <UDropdownMenu :items="sortMenuItems" :ui="{ content: 'w-44' }">
            <button
              type="button"
              :class="[segmentBaseClass, segmentIdleClass, 'gap-1 px-2 text-xs font-medium']"
              title="Sort by"
            >
              {{ sortLabel }}
              <Icon icon="mdi:chevron-down" class="w-3.5 h-3.5" />
            </button>
          </UDropdownMenu>

          <button
            type="button"
            :class="[segmentBaseClass, segmentIdleClass, 'w-8']"
            :title="
              sortDirection === SortDirection.Asc ? 'Switch to descending' : 'Switch to ascending'
            "
            :aria-label="sortDirection === SortDirection.Asc ? 'Ascending' : 'Descending'"
            @click="toggleSortDirection"
          >
            <Icon
              :icon="
                sortDirection === SortDirection.Asc ? 'mdi:sort-ascending' : 'mdi:sort-descending'
              "
              class="w-4 h-4"
            />
          </button>
        </div>

        <!-- Display: view mode + page size -->
        <div :class="pillClass">
          <button
            type="button"
            :class="[
              segmentBaseClass,
              'w-8',
              viewMode === 'grid' ? segmentActiveClass : segmentIdleClass,
            ]"
            aria-label="Grid view"
            :aria-pressed="viewMode === 'grid'"
            @click="viewMode = 'grid'"
          >
            <Icon icon="mdi:view-grid" class="w-4 h-4" />
          </button>
          <button
            type="button"
            :class="[
              segmentBaseClass,
              'w-8',
              viewMode === 'list' ? segmentActiveClass : segmentIdleClass,
            ]"
            aria-label="List view"
            :aria-pressed="viewMode === 'list'"
            @click="viewMode = 'list'"
          >
            <Icon icon="mdi:view-list" class="w-4 h-4" />
          </button>

          <span aria-hidden="true" class="mx-0.5 h-4 w-px bg-gray-300/70 dark:bg-gray-600/70" />

          <UDropdownMenu :items="pageSizeMenuItems" :ui="{ content: 'w-40' }">
            <button
              type="button"
              :class="[segmentBaseClass, segmentIdleClass, 'gap-1 px-2 text-xs font-medium']"
              title="Items per page"
            >
              <span class="tabular-nums">{{ pageSize }}</span>
              <span>per page</span>
              <Icon icon="mdi:chevron-down" class="w-3.5 h-3.5" />
            </button>
          </UDropdownMenu>
        </div>

        <div class="mx-1 h-5 w-px shrink-0 bg-gray-300/70 dark:bg-gray-600/70" />

        <!-- Search -->
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          class="shrink-0 gap-1.5 rounded-lg"
          aria-label="Search"
          title="Quick search (⌘/)"
          @click="quickSearch"
        >
          <Icon icon="mdi:magnify" class="w-4 h-4" />
          <kbd
            class="rounded border border-gray-300/70 px-1 font-mono text-[11px] leading-4 text-gray-600 dark:border-gray-600/70 dark:text-gray-400"
          >
            ⌘/
          </kbd>
        </UButton>
      </div>

      <!-- Compact toolbar also handles space taken by side panels. -->
      <div class="flex @3xl/explorer:hidden w-full items-center gap-2 px-3 py-2">
        <!-- Upload — opens action sheet -->
        <UButton
          color="primary"
          size="sm"
          class="shrink-0 rounded-lg font-semibold"
          aria-label="Upload"
          @click="isMobileUploadSheetOpen = true"
        >
          <Icon icon="mdi:upload" class="w-4 h-4 mr-1.5" />
          Upload
        </UButton>

        <UButton
          variant="outline"
          color="neutral"
          size="sm"
          class="shrink-0 rounded-lg"
          aria-label="Search files"
          @click="quickSearch"
        >
          <Icon icon="mdi:magnify" class="w-4 h-4 mr-1.5" />
          Search
        </UButton>

        <div class="flex-1" />

        <UDropdownMenu :items="mobileOverflowItems" :ui="{ content: 'w-52' }">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            square
            class="justify-center rounded-lg"
            aria-label="More options"
          >
            <Icon icon="mdi:dots-horizontal" class="w-5 h-5" />
          </UButton>
        </UDropdownMenu>
      </div>
    </div>

    <!-- mobile upload drawer -->
    <UDrawer
      v-model:open="isMobileUploadSheetOpen"
      direction="bottom"
      :ui="{
        content: `${glassDrawerContent} rounded-t-2xl border-t border-gray-200/70 dark:border-gray-700/70`,
      }"
    >
      <template #content>
        <div class="px-4 pt-2 pb-3">
          <p class="text-xs font-medium uppercase tracking-widest text-gray-400 dark:text-gray-600">
            Upload to {{ currentDirName ?? "Home" }}
          </p>
        </div>

        <ul
          class="px-2 space-y-1"
          :style="{ paddingBottom: 'max(1rem, env(safe-area-inset-bottom))' }"
        >
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('File')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="mdi:file-outline" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">File</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload one or more files
                </p>
              </div>
            </button>
          </li>
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('Directory')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="mdi:folder-outline" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">Folder</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload an entire folder
                </p>
              </div>
            </button>
          </li>
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('Archive')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="formkit:zip" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">Archive</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload and extract a zip
                </p>
              </div>
            </button>
          </li>
        </ul>
      </template>
    </UDrawer>

    <!-- explorer panel -->
    <div
      class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-none border border-x-0 border-t-0 border-gray-200/70 frosted-glass glass-surface md:rounded-xl md:border-x md:border-t dark:border-gray-700/70"
    >
      <!-- breadcrumb row -->
      <div
        class="flex min-w-0 shrink-0 items-center gap-1 border-b border-gray-200/70 px-3 py-2 dark:border-gray-700/70"
      >
        <UButton
          size="xs"
          variant="ghost"
          color="neutral"
          square
          class="justify-center rounded-md"
          :disabled="!canGoBack"
          :class="!canGoBack ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'"
          :title="canGoBack ? 'Go back' : 'No previous location'"
          aria-label="Back"
          @click="navigateBack()"
        >
          <Icon icon="mdi:arrow-left" class="w-4 h-4" />
        </UButton>
        <UButton
          size="xs"
          variant="ghost"
          color="neutral"
          square
          class="justify-center rounded-md"
          :disabled="!canGoForward"
          :class="!canGoForward ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'"
          :title="canGoForward ? 'Go forward' : 'No next location'"
          aria-label="Forward"
          @click="navigateForward()"
        >
          <Icon icon="mdi:arrow-right" class="w-4 h-4" />
        </UButton>

        <div class="mx-1 h-5 w-px bg-gray-300/70 dark:bg-gray-600/70" />

        <BreadcrumbNavigation :items="breadcrumbs" @navigate="handleNavigate" />
      </div>

      <!-- mobile background refresh indicator -->
      <Transition name="fade-status">
        <div
          v-if="isBackgroundLoading"
          class="flex @3xl/explorer:hidden items-center gap-1.5 border-b border-gray-200/70 px-4 py-1 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400 select-none"
          aria-live="polite"
        >
          <BlocksSpinner :size="11" aria-label="Refreshing" class="opacity-50" />
          <span>Refreshing…</span>
        </div>
      </Transition>

      <!-- content area -->
      <UContextMenu
        :items="explorerMenuItems"
        :ui="{ content: 'lg:min-w-56' }"
        class="flex min-h-0 min-w-0 flex-1 flex-col"
        v-model:open="contextMenuOpen"
        @update:open="handleMenuOpenChange"
      >
        <div class="relative flex min-h-0 min-w-0 flex-1 flex-col">
          <!-- The overlay sits outside the scroller so it stays in view at any scroll offset. -->
          <Transition
            enter-active-class="transition-opacity duration-150 ease-out"
            enter-from-class="opacity-0"
            leave-active-class="transition-opacity duration-100 ease-in"
            leave-to-class="opacity-0"
          >
            <div
              v-if="isOverDropZone"
              class="pointer-events-none absolute inset-0 z-50 flex items-end justify-center bg-primary/5 p-4 ring-2 ring-inset ring-primary"
              aria-hidden="true"
            >
              <div
                class="flex max-w-full items-center gap-3 rounded-lg border border-gray-200/70 px-3 py-2 shadow-lg frosted-glass glass-surface-strong dark:border-gray-700/70"
              >
                <div
                  class="flex shrink-0 items-center justify-center rounded-md bg-primary/10 p-1.5"
                >
                  <Icon :icon="dropIcon" class="h-5 w-5 text-primary" />
                </div>

                <div class="min-w-0">
                  <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                    {{ dropLabel }}
                  </p>
                  <p class="truncate text-xs text-gray-600 dark:text-gray-400">
                    Upload to
                    <strong class="font-semibold text-gray-900 dark:text-gray-100">
                      {{ currentDirName ?? "Home" }}
                    </strong>
                  </p>
                </div>
              </div>
            </div>
          </Transition>

          <div
            ref="containerRef"
            class="relative flex-1 overflow-auto data-[drop-active=valid]:ring-2 data-[drop-active=valid]:ring-inset data-[drop-active=valid]:ring-primary"
            data-drop-scroll
            data-drop-kind="background"
            :data-drop-dir-id="toDropId(currentDirId)"
            :data-drop-dir-name="currentDirName ?? 'Home'"
            @contextmenu.capture="handleContextMenuCapture"
            @pointerdown.capture="handleContextMenuPointerDown"
            @dragenter="onDragEnter"
            @dragleave="onDragLeave"
            @dragover="onDragOver"
            @drop="handleDropEvent"
          >
            <div
              v-if="marqueeBox"
              class="pointer-events-none absolute z-20 rounded-sm border border-primary/60 bg-primary/10"
              :style="{
                height: `${marqueeBox.bottom - marqueeBox.top}px`,
                left: `${marqueeBox.left}px`,
                top: `${marqueeBox.top}px`,
                width: `${marqueeBox.right - marqueeBox.left}px`,
              }"
              aria-hidden="true"
            />

            <!-- empty state: no folders or files, and nothing still loading -->
            <ExplorerEmptyState
              v-if="isExplorerEmpty"
              @upload="handleFileUpload('File')"
              @new-folder="createNewDirectory"
            />

            <template v-else>
              <!-- grid view -->
              <div v-if="viewMode === 'grid'" class="p-4">
                <GridPlaceholder v-if="showDirSkeleton" />
                <div v-else-if="directoriesList.length > 0" class="mb-8 flex flex-col">
                  <div class="mb-3 flex items-center gap-2">
                    <h3 class="text-xs font-semibold text-gray-600 dark:text-gray-400">Folders</h3>
                    <span
                      class="rounded-full bg-gray-500/10 px-1.5 text-[11px] font-medium tabular-nums text-gray-600 dark:bg-gray-400/10 dark:text-gray-400"
                    >
                      {{ directoriesData?.totalCount ?? directoriesList.length }}
                    </span>
                  </div>
                  <div class="grid gap-3" :style="gridStyle">
                    <DirectoryItem
                      v-for="dir in directoriesList"
                      :key="dir.id"
                      :data="dir"
                      :view-mode="viewMode"
                      :is-selected="isDirectorySelected(dir.id)"
                      @navigate="handleNavigate"
                      @click="handleItemClick($event, dir.id, 'directory')"
                      :class="{
                        'opacity-40 grayscale-30 transition-opacity': isDimmed(dir.id, 'directory'),
                      }"
                    />
                  </div>
                  <ExplorerLoadMore
                    v-if="directoriesData?.hasNext"
                    label="Show more folders"
                    :shown="directoriesList.length"
                    :total="directoriesData?.totalCount ?? directoriesList.length"
                    :loading="areDirectoriesLoading"
                    @load="loadMoreDirs"
                  />
                </div>

                <GridPlaceholder v-if="showFileSkeleton" />
                <div v-else-if="filesList.length > 0" class="mb-6 flex flex-col flex-1">
                  <div class="mb-3 flex items-center gap-2 pt-2">
                    <h3 class="text-xs font-semibold text-gray-600 dark:text-gray-400">Files</h3>
                    <span
                      class="rounded-full bg-gray-500/10 px-1.5 text-[11px] font-medium tabular-nums text-gray-600 dark:bg-gray-400/10 dark:text-gray-400"
                    >
                      {{ filesData?.totalCount ?? filesList.length }}
                    </span>
                  </div>
                  <div class="grid gap-3" :style="gridStyle">
                    <FileItem
                      v-for="file in filesList"
                      :key="file.fileId"
                      :data="file"
                      :view-mode="viewMode"
                      :is-selected="isFileSelected(file.fileId)"
                      @open-details="activeDetailsFile = $event"
                      @tooltip-enter="handleTooltipEnter"
                      @tooltip-leave="handleTooltipLeave"
                      :described-by="tooltipDescribedBy(file.fileId)"
                      @click="handleItemClick($event, file.fileId, 'file')"
                      :class="{
                        'opacity-40 grayscale-30 transition-opacity': isDimmed(file.fileId, 'file'),
                      }"
                    />
                  </div>
                  <ExplorerLoadMore
                    v-if="filesData?.hasNext"
                    label="Show more files"
                    :shown="filesList.length"
                    :total="filesData?.totalCount ?? filesList.length"
                    :loading="areFilesLoading"
                    @load="loadMoreFiles"
                  />
                </div>
              </div>

              <!-- list view -->
              <div v-else class="flex flex-col pb-2">
                <ListPlaceholder v-if="showDirSkeleton" />
                <div v-else-if="directoriesList.length > 0" class="flex flex-col">
                  <div class="flex items-center gap-2 px-4 pt-4 pb-2">
                    <h3 class="text-xs font-semibold text-gray-600 dark:text-gray-400">Folders</h3>
                    <span
                      class="rounded-full bg-gray-500/10 px-1.5 text-[11px] font-medium tabular-nums text-gray-600 dark:bg-gray-400/10 dark:text-gray-400"
                    >
                      {{ directoriesData?.totalCount ?? directoriesList.length }}
                    </span>
                  </div>
                  <div
                    class="sticky top-0 z-10 grid h-8 grid-cols-[minmax(0,1fr)_16px] items-center gap-4 border-b border-gray-200/70 px-4 text-xs font-semibold text-gray-600 frosted-glass glass-surface-strong @min-[720px]/explorer:grid-cols-[minmax(0,1fr)_112px_16px] dark:border-gray-700/70 dark:text-gray-400"
                  >
                    <span>Name</span>
                    <span class="hidden @min-[720px]/explorer:block">Modified</span>
                    <span />
                  </div>
                  <DirectoryItem
                    v-for="dir in directoriesList"
                    :key="dir.id"
                    :data="dir"
                    :view-mode="viewMode"
                    :is-selected="isDirectorySelected(dir.id)"
                    @navigate="handleNavigate"
                    @click="handleItemClick($event, dir.id, 'directory')"
                    :class="{
                      'opacity-40 grayscale-30 transition-opacity': isDimmed(dir.id, 'directory'),
                    }"
                  />
                  <ExplorerLoadMore
                    v-if="directoriesData?.hasNext"
                    label="Show more folders"
                    :shown="directoriesList.length"
                    :total="directoriesData?.totalCount ?? directoriesList.length"
                    :loading="areDirectoriesLoading"
                    @load="loadMoreDirs"
                  />
                </div>

                <ListPlaceholder v-if="showFileSkeleton" />
                <div
                  v-else-if="filesList.length > 0"
                  class="flex flex-col"
                  :class="{ 'mt-4': (directoriesData?.items?.length ?? 0) > 0 }"
                >
                  <div
                    class="flex items-center gap-2 px-4 pb-2"
                    :class="(directoriesData?.items?.length ?? 0) === 0 ? 'pt-4' : 'pt-2'"
                  >
                    <h3 class="text-xs font-semibold text-gray-600 dark:text-gray-400">Files</h3>
                    <span
                      class="rounded-full bg-gray-500/10 px-1.5 text-[11px] font-medium tabular-nums text-gray-600 dark:bg-gray-400/10 dark:text-gray-400"
                    >
                      {{ filesData?.totalCount ?? filesList.length }}
                    </span>
                  </div>
                  <div
                    class="sticky top-0 z-10 grid h-8 grid-cols-[minmax(0,1fr)_56px_88px] items-center gap-4 border-b border-gray-200/70 px-4 text-xs font-semibold text-gray-600 frosted-glass glass-surface-strong @min-[720px]/explorer:grid-cols-[minmax(0,1fr)_56px_112px_88px] dark:border-gray-700/70 dark:text-gray-400"
                  >
                    <span>Name</span>
                    <span>Type</span>
                    <span class="hidden @min-[720px]/explorer:block">Modified</span>
                    <span class="text-right">Size</span>
                  </div>
                  <FileItem
                    v-for="file in filesList"
                    :key="file.fileId"
                    :data="file"
                    :view-mode="viewMode"
                    :is-selected="isFileSelected(file.fileId)"
                    @open-details="activeDetailsFile = $event"
                    @tooltip-enter="handleTooltipEnter"
                    @tooltip-leave="handleTooltipLeave"
                    :described-by="tooltipDescribedBy(file.fileId)"
                    @click="handleItemClick($event, file.fileId, 'file')"
                    :class="{
                      'opacity-40 grayscale-30 transition-opacity': isDimmed(file.fileId, 'file'),
                    }"
                  />
                </div>
                <ExplorerLoadMore
                  v-if="filesData?.hasNext"
                  label="Show more files"
                  :shown="filesList.length"
                  :total="filesData?.totalCount ?? filesList.length"
                  :loading="areFilesLoading"
                  @load="loadMoreFiles"
                />
              </div>
            </template>
          </div>
        </div>
      </UContextMenu>

      <!-- status bar -->
      <div
        class="flex shrink-0 items-center justify-between border-t border-gray-200/70 px-4 py-2 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
      >
        <span class="tabular-nums">
          {{ totalItemCount }} item{{ totalItemCount === 1 ? "" : "s" }}
        </span>
        <span class="flex items-center gap-3">
          <span
            v-if="pendingMoveCount > 0"
            class="flex items-center gap-1.5 tabular-nums"
            aria-live="polite"
          >
            <BlocksSpinner :size="11" aria-hidden="true" class="opacity-50" />
            Moving {{ pendingMoveCount }} item{{ pendingMoveCount === 1 ? "" : "s" }}…
          </span>
          <span v-if="selectedCount > 0" class="tabular-nums">{{ selectedCount }} selected</span>
        </span>
      </div>
    </div>

    <!-- shared file details drawer — single instance for all FileItem triggers -->
    <FileDetailsDrawer
      v-model:file="activeDetailsFile"
      @file-trashed="handleFileTrashed"
      @file-restored="refreshDir"
    />

    <!-- shared folder details drawer — single instance for all DirectoryItem triggers -->
    <FolderDetailsDrawer
      v-model:directory="activeDetailsDirectory"
      :favorite-pending="isFavoritePending"
      @favorite="handleDirectoryFavorite"
      @navigate="handleNavigate"
      @rename="handleDirectoryRename"
      @move="openTransferModal('move')"
      @download="(ids) => handleDownload('dir', ids[0])"
      @delete="handleDelete"
    />

    <!-- shared rich file tooltip — single controller for all FileItem triggers -->
    <UTooltip
      :reference="tooltipAnchor ?? undefined"
      v-model:open="tooltipOpen"
      :delay-duration="600"
      :disabled="explorerIsMobile"
      :content="fileTooltipContent"
      :ui="fileTooltipUi"
    >
      <template #content>
        <div
          v-if="tooltipFile"
          id="explorer-file-tooltip"
          @mouseenter="fileTooltip.contentEnter()"
          @mouseleave="fileTooltip.contentLeave()"
        >
          <FileTooltipCard :data="tooltipFile" />
        </div>
      </template>
    </UTooltip>
  </div>
</template>

<script setup lang="ts">
import type { BreadcrumbItem, DropdownMenuItem } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { useQuery, useQueryCache } from "@pinia/colada";
import { breakpointsTailwind, useBreakpoints, useEventListener } from "@vueuse/core";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRouter } from "vue-router";

import type { DirectorySummaryDto } from "@/api/directory";
import type { ExplorerItemKind } from "@/features/explorer/drag/geometry";
import type { NavItem } from "@/types/nav-item";

import { type FileResult } from "@/api/file";
import BlocksSpinner from "@/components/common/BlockSpinner.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import AdvancedSearchModal from "@/components/dashboard/file-system/Modals/AdvancedSearchModal.vue";
import CreateDirectoryModal from "@/components/dashboard/file-system/Modals/CreateDirectoryModal.vue";
import FileTransferModal, {
  type TransferResult,
} from "@/components/dashboard/file-system/Modals/Filetransfermodal.vue";
import QuickSearchModal from "@/components/dashboard/file-system/Modals/QuickSearchModal.vue";
import ShareLinkModal from "@/components/dashboard/file-system/Modals/ShareLinkModal.vue";
import UpdateDirectoryModal from "@/components/dashboard/file-system/Modals/UpdateDirectoryModal.vue";
import UpdateFileModal from "@/components/dashboard/file-system/Modals/UpdateFileModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useDropZone } from "@/composables/useDropZone";
import { useExplorerCommandGuard } from "@/composables/useExplorerCommands";
import {
  type MoveRecord,
  toDropId,
  useExplorerDragSession,
} from "@/composables/useExplorerDragSession";
import { useExplorerItemDrag } from "@/composables/useExplorerItemDrag";
import { useExplorerMarquee } from "@/composables/useExplorerMarquee";
import { useExplorerReveal } from "@/composables/useExplorerReveal";
import { useFileDownload } from "@/composables/useFileDownload";
import { useFileExplorer } from "@/composables/useFileExplorer";
import { type FileTooltipEnterKind, useFileTooltip } from "@/composables/useFileTooltip";
import { useLazyModal } from "@/composables/useLazyOverlay";
import { useUploadLauncher } from "@/composables/useUploadLauncher";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  type ExplorerMenuActions,
  type ExplorerMenuSnapshot,
  buildExplorerMenuItems,
  resolveContextMenuTarget,
} from "@/features/explorer/context-menu";
import { EXPLORER_PAGE_SIZES } from "@/features/explorer/preferences";
import { describeExplorerItems, quoted } from "@/features/toast/format";
import {
  copyDirectory,
  deleteDirectory,
  moveDirectories,
  setDirectoryFavorite,
} from "@/mutations/directories";
import { copyFiles, deleteFiles, moveFiles } from "@/mutations/files";
import { useDirectoryStore } from "@/stores/directory";
import { useFileStore } from "@/stores/file";
import { useSettingsStore } from "@/stores/settings";
import { useTabStore } from "@/stores/tab";
import { getFileIcon } from "@/utils/files/icons";
import { logger } from "@/utils/logger";
import { glassDrawerContent } from "@/utils/modal-ui";

import BreadcrumbNavigation from "./BreadcrumbNavigation.vue";
import DirectoryItem from "./DirectoryItem.vue";
import ExplorerEmptyState from "./ExplorerEmptyState.vue";
import ExplorerLoadMore from "./ExplorerLoadMore.vue";
import FileDetailsDrawer from "./FileDetailsDrawer.vue";
import FileItem from "./FileItem.vue";
import FileTooltipCard from "./FileTooltipCard.vue";
import FolderDetailsDrawer from "./FolderDetailsDrawer.vue";

const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
const settingsStore = useSettingsStore();
const tabStore = useTabStore();
const appToast = useAppToast("files");
const router = useRouter();

const { tabId } = defineProps<{ tabId: string }>();

const {
  currentDirId,
  directoriesQuery,
  filesQuery,
  directoriesList,
  filesList,
  viewMode,
  filePagination,
  dirPagination,
  sortBy,
  sortDirection,
  pageSize,
  setSortBy,
  toggleSortDirection,
  setPageSize,
  lastSelected,
  selectedDirectories,
  selectedFiles,
  pathQuery,
  loadMoreDirs,
  loadMoreFiles,
  navigateTo,
  refreshDir,
  canGoBack,
  canGoForward,
  navigateBack,
  navigateForward,
  toggleSelect,
  isDirectorySelected,
  isFileSelected,
  clearSelection,
  selectRange,
} = useFileExplorer();
const dragSession = useExplorerDragSession();
const { downloadFile, downloadBulk } = useFileDownload();
const { canHandleCommand } = useExplorerCommandGuard(tabId);

const { mutateAsync: copyFilesMutate } = copyFiles();
const { mutateAsync: copyDirectoryMutate } = copyDirectory();
const { mutateAsync: moveFilesMutate } = moveFiles();
const { mutateAsync: moveDirectoriesMutate } = moveDirectories();
const { mutateAsync: deleteFilesMutate } = deleteFiles();
const { mutateAsync: deleteDirectoryMutate } = deleteDirectory();

const { mutateAsync: favoriteDirectoryMutate, isLoading: isFavoritePending } =
  setDirectoryFavorite();

const { data: directoriesData, isLoading: areDirectoriesLoading } = directoriesQuery;
const { data: filesData, isLoading: areFilesLoading } = filesQuery;

//copy tracking lives in the shared session-only file store alongside the
//clipboard selection so a cut started in one tab still moves when pasted in
//another tab (the directory store persists, so it cannot own the mode)
const copyMode = computed({
  get: () => fileStore.copyMode,
  set: (val: boolean) => {
    fileStore.copyMode = val;
  },
});

// skeleton tracking

const dirHasLoaded = ref(false);
const fileHasLoaded = ref(false);

watch(
  directoriesData,
  (val) => {
    if (val !== undefined) dirHasLoaded.value = true;
  },
  { immediate: true },
);
watch(
  filesData,
  (val) => {
    if (val !== undefined) fileHasLoaded.value = true;
  },
  { immediate: true },
);

const showDirSkeleton = computed(() => areDirectoriesLoading.value && !dirHasLoaded.value);
const showFileSkeleton = computed(() => areFilesLoading.value && !fileHasLoaded.value);

const isExplorerEmpty = computed(
  () =>
    !showDirSkeleton.value &&
    !showFileSkeleton.value &&
    directoriesList.value.length === 0 &&
    filesList.value.length === 0,
);

const isBackgroundLoading = computed(
  () =>
    (areDirectoriesLoading.value && dirHasLoaded.value) ||
    (areFilesLoading.value && fileHasLoaded.value),
);

// last-refreshed timer

const lastRefreshedAt = ref<Date | null>(null);
const lastRefreshedLabel = ref<string>("");
let labelTimer: ReturnType<typeof setInterval> | null = null;

const formatRelative = (date: Date): string => {
  const secs = Math.floor((Date.now() - date.getTime()) / 1000);
  if (secs < 5) return "just now";
  if (secs < 60) return `${secs}s ago`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  return `${Math.floor(mins / 60)}h ago`;
};

const startLabelTimer = (date: Date) => {
  if (labelTimer) clearInterval(labelTimer);
  lastRefreshedAt.value = date;
  lastRefreshedLabel.value = formatRelative(date);
  labelTimer = setInterval(() => {
    lastRefreshedLabel.value = formatRelative(date);
  }, 10_000);
};

watch(areDirectoriesLoading, (loading) => {
  if (!loading && dirHasLoaded.value) startLabelTimer(new Date());
});

// tab title sync

const currentDirName = computed<string | undefined>(() => {
  if (!currentDirId.value) return undefined;
  const parts = pathQuery.data.value?.pathParts;
  return parts?.[parts.length - 1]?.name;
});

watch(
  currentDirName,
  (name) => {
    tabStore.updateTabTitle(tabId, name ?? "Home");
  },
  { immediate: true },
);

const queryCache = useQueryCache();
const isDisposed = ref(false);

const invalidateDir = (dirId: string | null) => {
  if (dirId) {
    queryCache.invalidateQueries({
      key: ["directories", "sub-directories", dirId],
    });
    queryCache.invalidateQueries({ key: ["files", "sub-files", dirId] });
    return;
  }
  queryCache.invalidateQueries({
    key: ["directories", "root-sub-directories"],
  });
  queryCache.invalidateQueries({ key: ["files", "root-sub-files"] });
};

// drop zone

const uploadLauncher = useUploadLauncher();

const {
  containerRef,
  dragHasDirectory,
  isOverDropZone,
  onDragEnter,
  onDragLeave,
  onDragOver,
  onDrop,
} = useDropZone();

const dropIcon = computed(() =>
  dragHasDirectory.value ? "mdi:folder-upload-outline" : "mdi:cloud-upload-outline",
);
const dropLabel = computed(() =>
  dragHasDirectory.value ? "Drop to upload this folder" : "Drop to upload files",
);

const handleDropEvent = async (event: DragEvent) => {
  const dropResult = await onDrop(event);
  const targetDirId = currentDirId.value ?? null;

  const uploadProps = {
    directoryId: currentDirId.value ?? undefined,
    directoryName: currentDirName.value,
  };
  const shouldRefresh = await uploadLauncher.openDrop(dropResult, uploadProps);

  if (shouldRefresh) {
    if (isDisposed.value) {
      invalidateDir(targetDirId);
      return;
    }
    refreshDir();
  }
};

// sort & upload state

const sortByOptions = [
  { label: "Name", value: SortBy.Name },
  { label: "Date Created", value: SortBy.CreatedAt },
  { label: "Date Modified", value: SortBy.UpdatedAt },
];

// Segments share one chrome so the order and display pills read as a single cluster.
const pillClass =
  "inline-flex shrink-0 items-center gap-0.5 rounded-lg border border-gray-300/70 bg-gray-500/5 p-0.5 dark:border-gray-600/70 dark:bg-white/5";
const segmentBaseClass =
  "flex h-7 cursor-pointer items-center justify-center rounded-md transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary";
const segmentIdleClass =
  "text-gray-600 hover:bg-gray-500/10 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-white/10 dark:hover:text-gray-100";
const segmentActiveClass = "bg-gray-900 text-white dark:bg-gray-100 dark:text-gray-900";

const selectedCount = computed(() => selectedFiles.value.size + selectedDirectories.value.size);

const totalItemCount = computed(
  () =>
    (directoriesData.value?.totalCount ?? directoriesList.value.length) +
    (filesData.value?.totalCount ?? filesList.value.length),
);

const sortLabel = computed(
  () => sortByOptions.find((option) => option.value === sortBy.value)?.label ?? "Name",
);

// Shared by the desktop pills and the compact overflow menu so both stay in step.
const sortMenuItems = computed<DropdownMenuItem[]>(() => [
  { label: "Sort by", type: "label" },
  ...sortByOptions.map((option): DropdownMenuItem => ({
    checked: sortBy.value === option.value,
    label: option.label,
    onUpdateChecked: () => setSortBy(option.value),
    type: "checkbox",
  })),
]);

const pageSizeMenuItems = computed<DropdownMenuItem[]>(() => [
  { label: "Items per page", type: "label" },
  ...EXPLORER_PAGE_SIZES.map((size): DropdownMenuItem => ({
    checked: pageSize.value === size,
    label: String(size),
    onUpdateChecked: () => setPageSize(size),
    type: "checkbox",
  })),
]);

const selectedUploadType = ref({ icon: "mdi:file-outline", label: "File" });

const uploadOptions = ref([
  {
    icon: "mdi:file-outline",
    label: "File",
    onSelect: () => handleFileUpload("File"),
  },
  {
    icon: "mdi:folder-outline",
    label: "Directory",
    onSelect: () => handleFileUpload("Directory"),
  },
  {
    icon: "formkit:zip",
    label: "Archive",
    onSelect: () => handleFileUpload("Archive"),
  },
] satisfies DropdownMenuItem[]);

// shared context menu — one controller for row and background menus. The
// snapshot captured below freezes target and selection at open time so later
// changes cannot retarget the dispatched actions.
const menuSnapshot = ref<ExplorerMenuSnapshot | null>(null);
const contextMenuOpen = ref(false);
const menuAnchorRow = ref<Element | null>(null);

const backgroundSnapshot = (): ExplorerMenuSnapshot => ({
  directoryIds: [...selectedDirectories.value],
  fileIds: [...selectedFiles.value],
  kind: "background",
  targetDirectory: null,
  targetFile: null,
  totalCount: selectedCount.value,
});

const explorerMenuActions: ExplorerMenuActions = {
  copySelection: () => {
    const snap = menuSnapshot.value;
    if (!snap) return;
    openTransferModal("copy", snap.fileIds, snap.directoryIds);
  },
  createDirectory: () => createNewDirectory(),
  deleteSelection: () => {
    const snap = menuSnapshot.value;
    if (!snap) return;
    handleDelete(snap.fileIds, snap.directoryIds);
  },
  downloadDirectory: () => {
    const snap = menuSnapshot.value;
    if (!snap) return;
    downloadBulk(snap.fileIds, snap.directoryIds);
  },
  downloadFile: () => {
    const snap = menuSnapshot.value;
    if (!snap) return;
    if (snap.fileIds.length === 1 && snap.directoryIds.length === 0) {
      downloadFile(snap.fileIds[0]);
    } else {
      downloadBulk(snap.fileIds, snap.directoryIds);
    }
  },
  favoriteDirectory: (directory) => {
    void handleDirectoryFavorite(directory);
  },
  isFavoritePending: () => isFavoritePending.value,
  moveSelection: () => {
    const snap = menuSnapshot.value;
    if (!snap) return;
    openTransferModal("move", snap.fileIds, snap.directoryIds);
  },
  openDirectory: (directoryId) => handleNavigate(directoryId),
  openDirectoryDetails: (directory) => {
    activeDetailsDirectory.value = directory;
  },
  openFileDetails: (file) => {
    activeDetailsFile.value = file;
  },
  refresh: () => refreshDir(),
  renameDirectory: (directoryId) => handleDirectoryRename(directoryId),
  renameFile: (fileId, originalName) => handleFileRename(fileId, originalName),
  shareFile: (fileId, fileName) => handleShare(fileId, fileName),
  uploadArchive: () => handleFileUpload("Archive"),
  uploadDirectory: () => handleFileUpload("Directory"),
  uploadFile: () => handleFileUpload("File"),
};

const explorerMenuItems = computed(() =>
  buildExplorerMenuItems(menuSnapshot.value ?? backgroundSnapshot(), explorerMenuActions),
);

const captureMenuTarget = (event: Event) => {
  // A context menu replaces the tooltip; never show both side by side.
  fileTooltip.dismiss();
  const resolved = resolveContextMenuTarget(event.target);
  if (resolved.kind === "background") {
    menuSnapshot.value = backgroundSnapshot();
    menuAnchorRow.value = null;
    return;
  }

  // Right-click rules: an unselected row becomes the selection; an existing
  // multi-selection is preserved. Ordinary/additive/range clicks are untouched.
  const selected =
    resolved.kind === "file"
      ? isFileSelected(resolved.id ?? "")
      : isDirectorySelected(resolved.id ?? "");
  if (!selected && resolved.id) {
    clearSelection();
    toggleSelect(resolved.id, resolved.kind);
    lastSelected.value = resolved.id;
  }

  const targetFile =
    resolved.kind === "file"
      ? (filesList.value.find((file) => file.fileId === resolved.id) ?? null)
      : null;
  const targetDirectory =
    resolved.kind === "directory"
      ? (directoriesList.value.find((dir) => dir.id === resolved.id) ?? null)
      : null;
  menuSnapshot.value = {
    directoryIds: [...selectedDirectories.value],
    fileIds: [...selectedFiles.value],
    kind: resolved.kind,
    targetDirectory,
    targetFile,
    totalCount: selectedCount.value,
  };
  menuAnchorRow.value = resolved.row;
};

const handleContextMenuCapture = (event: Event) => {
  captureMenuTarget(event);
};

const handleContextMenuPointerDown = (event: PointerEvent) => {
  if (event.button === 2 || event.pointerType === "touch") captureMenuTarget(event);
};

const handleMenuOpenChange = (isOpen: boolean) => {
  if (isOpen) return;
  const row = menuAnchorRow.value;
  menuAnchorRow.value = null;
  if (row instanceof HTMLElement && row.isConnected && document.activeElement === document.body) {
    row.focus();
  }
};

const closeContextMenu = () => {
  contextMenuOpen.value = false;
  menuSnapshot.value = null;
  menuAnchorRow.value = null;
};

// drag selection and drag-to-move

const pendingMoveCount = dragSession.pendingMoveCount;

const { marqueeBox, isSelecting: isMarqueeSelecting } = useExplorerMarquee({
  container: containerRef,
  enabled: () => !contextMenuOpen.value,
  onStart: () => fileTooltip.dismiss(),
  selectedDirectories,
  selectedFiles,
});

const chipFor = (id: string, kind: ExplorerItemKind) => {
  if (kind === "file") {
    const file = filesList.value.find((item) => item.fileId === id);
    const name = file?.fileName ?? "File";

    return { icon: getFileIcon(name), id, kind, name };
  }

  const directory = directoriesList.value.find((item) => item.id === id);

  return { icon: "mdi:folder", id, kind, name: directory?.name ?? "Folder" };
};

useExplorerItemDrag({
  addToSelection: (id, kind) => {
    toggleSelect(id, kind);
    lastSelected.value = id;
  },
  chipFor,
  container: containerRef,
  enabled: () => !contextMenuOpen.value,
  isSelected: (id, kind) => {
    if (kind === "file") return isFileSelected(id);

    return isDirectorySelected(id);
  },
  onPickUp: () => fileTooltip.dismiss(),
  origin: () => ({
    id: currentDirId.value,
    name: currentDirName.value ?? "Home",
  }),
  selectOnly: (id, kind) => {
    clearSelection();
    toggleSelect(id, kind);
    lastSelected.value = id;
  },
  selection: () => ({
    directoryIds: [...selectedDirectories.value],
    fileIds: [...selectedFiles.value],
  }),
});

const removeMovedItems = (record: MoveRecord) => {
  if (record.originId !== currentDirId.value) return;

  const movedFiles = new Set(record.fileIds);
  const movedDirectories = new Set(record.directoryIds);

  if (movedFiles.size > 0) {
    filesList.value = filesList.value.filter((file) => !movedFiles.has(file.fileId));
    selectedFiles.value = new Set([...selectedFiles.value].filter((id) => !movedFiles.has(id)));
  }

  if (movedDirectories.size > 0) {
    directoriesList.value = directoriesList.value.filter((dir) => !movedDirectories.has(dir.id));
    selectedDirectories.value = new Set(
      [...selectedDirectories.value].filter((id) => !movedDirectories.has(id)),
    );
  }
};

// A failed move restores the listing from its first page, because items that
// were removed optimistically may belong to a page that is no longer active.
const reloadAfterFailedMove = (record: MoveRecord) => {
  if (record.originId !== currentDirId.value) return;

  dirPagination.value.paginationParams.page = 1;
  filePagination.value.paginationParams.page = 1;
  refreshDir();
};

dragSession.onMoved(removeMovedItems);
dragSession.onMoveFailed(reloadAfterFailedMove);

// mobile upload sheet

const isMobileUploadSheetOpen = ref(false);

const mobileUpload = (type: "File" | "Directory" | "Archive") => {
  isMobileUploadSheetOpen.value = false;
  handleFileUpload(type);
};

const mobileOverflowItems = computed(() => [
  ...sortMenuItems.value,
  { type: "separator" as const },
  {
    icon: sortDirection.value === SortDirection.Asc ? "mdi:sort-ascending" : "mdi:sort-descending",
    label: sortDirection.value === SortDirection.Asc ? "Ascending" : "Descending",
    onSelect: () => toggleSortDirection(),
  },
  { type: "separator" as const },
  ...pageSizeMenuItems.value,
  { type: "separator" as const },
  {
    icon: viewMode.value === "grid" ? "mdi:view-list" : "mdi:view-grid",
    label: viewMode.value === "grid" ? "Switch to List view" : "Switch to Grid view",
    onSelect: () => {
      viewMode.value = viewMode.value === "grid" ? "list" : "grid";
    },
  },
  { type: "separator" as const },
  {
    icon: "mdi:folder-plus",
    label: "New Folder",
    onSelect: () => createNewDirectory(),
  },
]);

// modals use lazy registrations created on open with destroyOnClose

const createDirectoryModal = useLazyModal<{ parentId: string | null }, boolean>(
  CreateDirectoryModal,
);
const updateDirectoryModal = useLazyModal<{ currentName: string; directoryId: string }, boolean>(
  UpdateDirectoryModal,
);
const advancedSearchModal = useLazyModal<Record<string, never>, string>(AdvancedSearchModal);
const quickSearchModal = useLazyModal<Record<string, never>, string>(QuickSearchModal);
const confirmModal = useLazyModal<Record<string, unknown>, boolean>(ConfirmModal);
const updateFileModal = useLazyModal<{ fileId: string; originalName: string }, boolean>(
  UpdateFileModal,
);
const fileTransferModal = useLazyModal<
  {
    dirChips: unknown[];
    directories: string[];
    fileChips: unknown[];
    files: string[];
    mode: "move" | "copy";
    originDirId: string | null;
    originDirName: string | undefined;
  },
  TransferResult | false
>(FileTransferModal);
const shareLinkModal = useLazyModal<{ fileId: string; fileName: string }, boolean>(ShareLinkModal);

const openTransferModal = async (
  mode: "move" | "copy",
  fileIds: string[] = [...selectedFiles.value],
  dirIds: string[] = [...selectedDirectories.value],
) => {
  // Build rich chip metadata from what we already have rendered
  const fileChips = fileIds.map((id) => {
    const f = filesList.value.find((file) => file.fileId === id);
    return {
      icon: f ? getFileIcon(f.fileName) : "mdi:file-outline",
      id,
      name: f?.fileName ?? "File",
      type: "file" as const,
    };
  });

  const dirChips = dirIds.map((id) => {
    const d = directoriesList.value.find((dir) => dir.id === id);
    return {
      icon: "mdi:folder-outline",
      id,
      name: d?.name ?? "Folder",
      type: "directory" as const,
    };
  });

  const instance = fileTransferModal.open({
    dirChips,
    directories: [...dirIds],
    fileChips,
    files: [...fileIds],
    mode,
    originDirId: currentDirId.value,
    originDirName: currentDirName.value,
  });

  const transfer = await instance.result;
  if (transfer) {
    if (isDisposed.value) {
      invalidateDir(currentDirId.value ?? null);
      return;
    }
    refreshDir();
    clearSelection();
  }
};

const advancedSearch = async () => {
  const instance = advancedSearchModal.open();
  const result = await instance.result;
  if (isDisposed.value) return;
  if (result === "close") return;
  else if (result === "root") handleNavigate(null);
  else if (typeof result === "string") handleNavigate(result);
};

const quickSearch = async () => {
  const instance = quickSearchModal.open();
  const result = await instance.result;

  if (isDisposed.value) return;
  if (result === "close") return;
  else if (result === "root") handleNavigate(null);
  else if (result === "advanced") advancedSearch();
  else if (typeof result === "string") handleNavigate(result);
};

// interaction handlers

const handleContainerClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  if (!target.closest("button")) clearSelection();
};

const gridStyle = computed(() => {
  // Keep the thumbnail and tile padding inside each column at every icon size.
  const thumbnailWidth = Math.round(settingsStore.gridIconSize * 1.5) * 1.4;
  const minWidth = Math.max(120, Math.ceil((thumbnailWidth + 32) / 8) * 8);
  return {
    gridTemplateColumns: `repeat(auto-fill, minmax(min(100%, ${minWidth}px), 1fr))`,
  };
});

const handleDirectoryRename = async (directoryId: string) => {
  const dir = directoriesList.value.find((d) => d.id === directoryId);
  const instance = updateDirectoryModal.open({
    currentName: dir?.name ?? "",
    directoryId,
  });
  await instance.result;
};

const handleFileRename = async (fileId: string, originalName: string) => {
  const instance = updateFileModal.open({ fileId, originalName });
  await instance.result;
};

const handleShare = (fileId: string, fileName: string) => {
  shareLinkModal.open({ fileId, fileName });
};

const handleDownload = async (type: "dir" | "file", emittedId?: string) => {
  const isMulti = selectedCount.value > 1;
  logger.log("emitted ids", emittedId);
  if (isMulti || type === "dir") {
    await downloadBulk([...selectedFiles.value], [...selectedDirectories.value]);
    return;
  }

  if (emittedId) await downloadFile(emittedId);
};

const handleCopy = () => {
  fileStore.selectedFiles = [...selectedFiles.value];
  fileStore.modificationOriginDirId = currentDirId.value;
  directoryStore.selectedDirectories = [...selectedDirectories.value];
  directoryStore.modificationOriginDirId = currentDirId.value;

  const fileCount = selectedFiles.value.size;
  const folderCount = selectedDirectories.value.size;
  if (fileCount + folderCount === 0) return;

  // Cut shares this clipboard step; copyMode is already set by the caller.
  const subject = describeExplorerItems(fileCount, folderCount);

  if (copyMode.value) {
    appToast.info(`Copied ${subject}`, {
      description: "Open a folder and paste to place the copies there.",
      source: "clipboard",
    });
    return;
  }

  appToast.info(`Cut ${subject}`, {
    description: "Open a folder and paste to move them there.",
    source: "clipboard",
  });
};

const currentFolderLabel = () => {
  if (currentDirName.value) return currentDirName.value;

  return currentDirId.value ? "this folder" : "Home";
};

// Names a single item outright; larger selections fall back to counts.
const describeSelection = (fileIds: string[], dirIds: string[]) => {
  const [onlyFile] = fileIds;
  const [onlyDir] = dirIds;

  if (fileIds.length === 1 && dirIds.length === 0 && onlyFile) {
    const name = filesList.value.find((file) => file.fileId === onlyFile)?.fileName;
    if (name) return quoted(name);
  }

  if (dirIds.length === 1 && fileIds.length === 0 && onlyDir) {
    const name = directoriesList.value.find((dir) => dir.id === onlyDir)?.name;
    if (name) return quoted(name);
  }

  return describeExplorerItems(fileIds.length, dirIds.length);
};

const openTrashAction = {
  icon: "i-lucide-arrow-up-right",
  label: "Open trash",
  onClick: () => {
    void router.push({ name: "trash" });
  },
};

const forceDeleteDirectories = (ids: string[]) =>
  Promise.all(
    ids.map((id) =>
      deleteDirectoryMutate({
        id,
        options: { force: true },
        originId: currentDirId.value,
      }),
    ),
  );

// Non-empty folders come back as 409 and need explicit consent before their contents go too.
const confirmNonEmptyDelete = async (ids: string[]) => {
  if (settingsStore.skipDeleteConfirmation) return true;

  const instance = confirmModal.open({
    alert: {
      color: "warning",
      icon: "i-lucide-triangle-alert",
      title: "All sub items will also be deleted",
    },
    confirmIcon: "i-lucide-trash-2",
    confirmLabel: "Delete anyway",
    dangerMode: true,
    description: `${ids.length} ${ids.length === 1 ? "directory" : "directories"} still contain files and will be deleted.`,
    title: "Delete directories?",
  });

  return (await instance.result) === true;
};

interface DirectoryDeleteOutcome {
  deleted: number;
  failure: unknown;
}

// Folders delete one by one so a non-empty one (409) can ask for consent without blocking the rest.
const deleteDirectoriesWithConsent = async (
  dirIds: string[],
): Promise<DirectoryDeleteOutcome | null> => {
  const results = await Promise.allSettled(
    dirIds.map((id) =>
      deleteDirectoryMutate({
        id,
        options: { force: false },
        originId: currentDirId.value,
      }),
    ),
  );

  const nonEmptyDirs: string[] = [];
  const outcome: DirectoryDeleteOutcome = { deleted: 0, failure: null };

  results.forEach((result, index) => {
    const id = dirIds[index];

    if (result.status === "fulfilled") {
      outcome.deleted += 1;
    } else if (result.reason?.status === 409 && id) {
      nonEmptyDirs.push(id);
    } else {
      outcome.failure ??= result.reason;
    }
  });

  if (nonEmptyDirs.length === 0) return outcome;

  const confirmed = await confirmNonEmptyDelete(nonEmptyDirs);
  if (isDisposed.value) return null;

  if (confirmed) {
    await forceDeleteDirectories(nonEmptyDirs);
    outcome.deleted += nonEmptyDirs.length;
  }

  return outcome;
};

const reportDeleteOutcome = (
  subject: string,
  requested: { dirs: number; files: number },
  deleted: { dirs: number; files: number },
) => {
  if (deleted.files + deleted.dirs === 0) return;

  const deletedAll = deleted.files === requested.files && deleted.dirs === requested.dirs;
  const deletedSubject = deletedAll ? subject : describeExplorerItems(deleted.files, deleted.dirs);

  appToast.success(`Moved ${deletedSubject} to the trash`, {
    actions: [openTrashAction],
    source: "trash",
  });
};

const handleDelete = async (
  fileIds: string[] = [...selectedFiles.value],
  dirIds: string[] = [...selectedDirectories.value],
) => {
  if (fileIds.length === 0 && dirIds.length === 0) return;

  const subject = describeSelection(fileIds, dirIds);
  let directoryOutcome: DirectoryDeleteOutcome | null = { deleted: 0, failure: null };

  try {
    if (fileIds.length > 0) {
      await deleteFilesMutate({
        ids: fileIds,
        directoryId: currentDirId.value ?? undefined,
      });
    }

    if (dirIds.length > 0) directoryOutcome = await deleteDirectoriesWithConsent(dirIds);
  } catch (err) {
    if (isDisposed.value) return;

    appToast.error(`Couldn't move ${subject} to the trash`, err, { source: "trash" });
    return;
  }

  if (isDisposed.value || !directoryOutcome) return;

  reportDeleteOutcome(
    subject,
    { dirs: dirIds.length, files: fileIds.length },
    { dirs: directoryOutcome.deleted, files: fileIds.length },
  );

  if (directoryOutcome.failure) {
    appToast.error("Some folders couldn't be moved to the trash", directoryOutcome.failure, {
      source: "trash",
    });
  }
};

const handleCut = async () => {
  const fileIds = [...fileStore.selectedFiles];
  const dirIds = [...directoryStore.selectedDirectories];
  if (fileIds.length === 0 && dirIds.length === 0) return;

  const subject = describeExplorerItems(fileIds.length, dirIds.length);
  const destination = currentFolderLabel();

  try {
    if (fileIds.length > 0) {
      await moveFilesMutate({
        destinationId: currentDirId.value,
        fileIds,
        originId: fileStore.modificationOriginDirId,
      });
      fileStore.selectedFiles = [];
      fileStore.modificationOriginDirId = null;
    }
    if (dirIds.length > 0) {
      await moveDirectoriesMutate({
        destinationId: currentDirId.value,
        directoryIds: dirIds,
        originId: directoryStore.modificationOriginDirId,
      });
      directoryStore.selectedDirectories = [];
      directoryStore.modificationOriginDirId = null;
    }
  } catch (err) {
    if (isDisposed.value) return;

    // The cut stays armed so pasting again retries the same items.
    appToast.error(`Couldn't move ${subject} to ${destination}`, err, {
      retry: handleCut,
      source: "move",
    });
    return;
  }

  copyMode.value = true;
  if (isDisposed.value) return;

  appToast.success(`Moved ${subject} to ${destination}`, { source: "move" });
};

const isCutFile = (id: string) => !copyMode.value && fileStore.selectedFiles.includes(id);

const isCutDirectory = (id: string) =>
  !copyMode.value && directoryStore.selectedDirectories.includes(id);

const isDragSource = (id: string) => dragSession.session.value?.sourceIds.has(id) ?? false;

const isDimmed = (id: string, kind: ExplorerItemKind) => {
  if (isDragSource(id)) return true;
  if (kind === "file") return isCutFile(id);

  return isCutDirectory(id);
};

const cancelCut = () => {
  if (copyMode.value) return;
  fileStore.selectedFiles = [];
  fileStore.modificationOriginDirId = null;
  directoryStore.selectedDirectories = [];
  directoryStore.modificationOriginDirId = null;
  copyMode.value = true;
  appToast.info("Cut cancelled", { description: "Nothing was moved.", source: "clipboard" });
};

const handlePaste = async () => {
  const fileIds = [...fileStore.selectedFiles];
  const dirIds = [...directoryStore.selectedDirectories];
  if (fileIds.length === 0 && dirIds.length === 0) return;

  const subject = describeExplorerItems(fileIds.length, dirIds.length);
  const destination = currentFolderLabel();

  try {
    if (fileIds.length > 0) {
      await copyFilesMutate({
        destinationId: currentDirId.value,
        fileIds,
        originId: fileStore.modificationOriginDirId,
      });
      fileStore.modificationOriginDirId = null;
    }
    if (dirIds.length > 0) {
      await Promise.all(
        dirIds.map(async (dir) => {
          await copyDirectoryMutate({
            destinationId: currentDirId.value,
            directoryId: dir,
            originId: directoryStore.modificationOriginDirId,
          });
        }),
      );
      directoryStore.modificationOriginDirId = null;
    }
  } catch (err) {
    if (isDisposed.value) return;

    appToast.error(`Couldn't paste ${subject} into ${destination}`, err, { source: "move" });
    return;
  }

  if (isDisposed.value) return;

  appToast.success(`Pasted ${subject} into ${destination}`, { source: "move" });
};

const handleFileUpload = async (type: "File" | "Directory" | "Archive") => {
  const option = uploadOptions.value.find((opt) => opt.label === type);
  if (option) selectedUploadType.value = { icon: option.icon, label: option.label };

  const targetDirId = currentDirId.value ?? null;
  const uploadProps = {
    directoryId: currentDirId.value ?? undefined,
    directoryName: currentDirName.value,
  };

  const shouldRefresh = await uploadLauncher.open(type, uploadProps);
  if (isDisposed.value) {
    if (shouldRefresh) invalidateDir(targetDirId);
    return;
  }
  if (shouldRefresh) {
    logger.log("refreshing");
    refreshDir();
    return;
  }
  if (!shouldRefresh && directoryStore.error) {
    appToast.error("Upload failed", directoryStore.error, { source: "upload" });
  }
};

const breadcrumbs = computed(() => {
  const items: NavItem[] = [{ icon: "i-heroicons-home", key: null, label: "Home" }];
  const path = pathQuery.data.value?.pathParts;
  if (path && path.length > 0) {
    items.push(...path.map((segment) => ({ key: segment.id, label: segment.name })));
  }
  return items;
});

const createNewDirectory = async () => {
  const instance = createDirectoryModal.open({ parentId: currentDirId.value });
  await instance.result;
};

const handleItemClick = (event: MouseEvent, id: string, type: "file" | "directory") => {
  const isCtrlOrCmd = event.ctrlKey || event.metaKey;
  const isShift = event.shiftKey;

  fileTooltip.dismiss();

  if (isShift && lastSelected.value) {
    selectRange(lastSelected.value, id);
  } else if (isCtrlOrCmd) {
    toggleSelect(id, type);
    lastSelected.value = id;
  } else {
    clearSelection();
    toggleSelect(id, type);
    lastSelected.value = id;
  }
};

const handleNavigate = (dirId: string | null) => {
  dirHasLoaded.value = false;
  fileHasLoaded.value = false;
  navigateTo(dirId);
  tabStore.setActiveDir(tabId, dirId);
};

const handleMouseNavigate = (event: MouseEvent) => {
  if (event.button !== 3 && event.button !== 4) return;
  event.preventDefault();
  event.stopPropagation();
  if (event.button === 3 && canGoBack.value) navigateBack();
  if (event.button === 4 && canGoForward.value) navigateForward();
};

const handleRenameSelected = () => {
  const fileCount = selectedFiles.value.size;
  const dirCount = selectedDirectories.value.size;

  // rename only makes sense for a single item
  if (fileCount + dirCount !== 1) return;

  if (fileCount === 1) {
    const [fileId] = selectedFiles.value;
    const file = filesList.value.find((f) => f.fileId === fileId);
    if (file) handleFileRename(fileId, file.fileName);
    return;
  }

  if (dirCount === 1) {
    const [dirId] = selectedDirectories.value;
    handleDirectoryRename(dirId);
  }
};

const handleDownloadSelected = async () => {
  const fileCount = selectedFiles.value.size;
  const dirCount = selectedDirectories.value.size;

  if (fileCount + dirCount === 0) return;

  // single file, no dirs → direct download
  if (fileCount === 1 && dirCount === 0) {
    const [fileId] = selectedFiles.value;
    await downloadFile(fileId);
    return;
  }

  // anything else (multi-file, dirs, or mixed) → bulk
  await downloadBulk([...selectedFiles.value], [...selectedDirectories.value]);
};

// shared file details drawer state — one drawer instance for every FileItem
const activeDetailsFile = ref<FileResult | null>(null);

const handleFileTrashed = (fileId: string) => {
  selectedFiles.value.delete(fileId);
  refreshDir();
};

// shared folder details drawer state — one drawer instance for every DirectoryItem
const activeDetailsDirectory = ref<DirectorySummaryDto | null>(null);

// Navigating away unmounts the listing, so the shared folder target closes
// with its explorer instead of lingering over unrelated content.
watch(currentDirId, () => {
  activeDetailsDirectory.value = null;
  fileTooltip.release();
  closeContextMenu();
});

// A deleted folder disappears from the listing after refresh. Close the
// shared drawer when its target is gone so actions cannot retarget stale data.
watch(directoriesList, (list) => {
  if (!dirHasLoaded.value) return;
  const target = activeDetailsDirectory.value;

  if (target) {
    activeDetailsDirectory.value = list.find((dir) => dir.id === target.id) ?? null;
  }

  const menuTarget = menuSnapshot.value?.targetDirectory;
  if (menuTarget && !list.some((dir) => dir.id === menuTarget.id)) closeContextMenu();
});

const handleDirectoryFavorite = async (directory: DirectorySummaryDto) => {
  if (isFavoritePending.value) return;

  try {
    const result = await favoriteDirectoryMutate({
      directoryId: directory.id,
      isStarred: !directory.isStarred,
    });

    if (isDisposed.value) return;

    directoriesList.value = directoriesList.value.map((item) => {
      if (item.id === result.directoryId) return { ...item, isStarred: result.isStarred };

      return item;
    });

    if (activeDetailsDirectory.value?.id === result.directoryId) {
      activeDetailsDirectory.value = {
        ...activeDetailsDirectory.value,
        isStarred: result.isStarred,
      };
    }

    const name = quoted(directory.name);

    appToast.success(
      result.isStarred ? `Added ${name} to favorites` : `Removed ${name} from favorites`,
      { source: "folders" },
    );
  } catch (error) {
    appToast.error(`Couldn't update favorites for ${quoted(directory.name)}`, error, {
      source: "folders",
    });
  }
};

// shared rich file tooltip — one controller for every FileItem. Rows report
// hover/focus intent with their trigger anchor; placement mirrors the previous
// per-row controllers (bottom in grid, top in list).
const explorerBreakpoints = useBreakpoints(breakpointsTailwind);
const explorerIsMobile = explorerBreakpoints.smaller("md");
const fileTooltip = useFileTooltip({
  disabled: computed(
    () => explorerIsMobile.value || dragSession.isDragging.value || isMarqueeSelecting.value,
  ),
});

// Template bindings do not unwrap nested refs, so the shared tooltip state
// is aliased to top-level bindings for the template below.
const tooltipAnchor = fileTooltip.anchor;
const tooltipFile = fileTooltip.file;
const tooltipOpen = fileTooltip.open;

const fileTooltipContent = computed(() => {
  if (viewMode.value === "grid") return { align: "center" as const, side: "bottom" as const };
  return { align: "center" as const, side: "top" as const };
});

const fileTooltipUi = computed(() => {
  // Controlled opens always report `instant-open` (Reka only marks `delayed-open`
  // on its internal trigger path), so both states carry the enter animation.
  if (viewMode.value === "grid") {
    return {
      content:
        "ring-0 h-auto p-0 rounded-md select-none data-[state=delayed-open]:animate-[scale-in_100ms_ease-out] data-[state=instant-open]:animate-[scale-in_100ms_ease-out] data-[state=closed]:animate-[scale-out_100ms_ease-in] origin-(--reka-tooltip-content-transform-origin) pointer-events-auto",
    };
  }
  return {
    content:
      "z-50 ring-0 h-auto p-0 rounded-md select-none data-[state=delayed-open]:animate-[scale-in_100ms_ease-out] data-[state=instant-open]:animate-[scale-in_100ms_ease-out] data-[state=closed]:animate-[scale-out_100ms_ease-in] origin-(--reka-tooltip-content-transform-origin) pointer-events-auto",
  };
});

const handleTooltipEnter = (file: FileResult, anchor: HTMLElement, kind: FileTooltipEnterKind) => {
  fileTooltip.enter(file, anchor, kind);
};

const handleTooltipLeave = (kind: FileTooltipEnterKind) => {
  fileTooltip.leave(kind);
};

const tooltipDescribedBy = (fileId: string) => {
  if (fileTooltip.open.value && fileTooltip.file.value?.fileId === fileId) {
    return "explorer-file-tooltip";
  }
  return null;
};

// View changes remount row triggers, so the shared anchor would dangle.
watch(viewMode, () => {
  fileTooltip.release();
});

// A removed file must never keep a stale anchored tooltip.
watch(filesList, (list) => {
  if (!fileHasLoaded.value) return;
  const target = fileTooltip.file.value;
  if (target && !list.some((file) => file.fileId === target.fileId)) fileTooltip.release();
  const menuTarget = menuSnapshot.value?.targetFile;
  if (menuTarget && !list.some((file) => file.fileId === menuTarget.fileId)) {
    closeContextMenu();
  }
});
const handleOpenDetailsSelected = () => {
  const fileCount = selectedFiles.value.size;
  const dirCount = selectedDirectories.value.size;

  if (fileCount + dirCount !== 1) return;

  if (fileCount === 1) {
    const [fileId] = selectedFiles.value;
    const file = filesList.value.find((f) => f.fileId === fileId);
    if (file) activeDetailsFile.value = file;
    return;
  }

  if (dirCount === 1) {
    const [dirId] = selectedDirectories.value;
    const dir = directoriesList.value.find((d) => d.id === dirId);
    if (dir) activeDetailsDirectory.value = dir;
  }
};

// Single explorer shortcut owner per D5. Every command applies to the current
// selection and runs only when this explorer may handle input.
const guarded = (command: () => void) => (event: KeyboardEvent) => {
  if (!canHandleCommand(event)) return;
  command();
};

// oxlint-disable-next-line sort-keys
defineShortcuts({
  // already present
  Delete: guarded(() => handleDelete()),
  alt_arrowleft: guarded(() => {
    if (canGoBack.value) navigateBack();
  }),
  alt_arrowright: guarded(() => {
    if (canGoForward.value) navigateForward();
  }),
  "meta_/": guarded(() => quickSearch()),
  meta_c: guarded(() => {
    copyMode.value = true;
    handleCopy();
  }),
  meta_v: guarded(() => {
    if (copyMode.value) handlePaste();
    else handleCut();
  }),
  meta_x: guarded(() => {
    copyMode.value = false;
    handleCopy();
  }),
  shift_k: guarded(() => quickSearch()),
  shift_l: guarded(() => advancedSearch()),

  // new — single-key, input-safe
  r: guarded(() => handleRenameSelected()),
  d: guarded(() => handleDownloadSelected()),
  n: guarded(() => createNewDirectory()),

  // new — F-key
  F2: guarded(() => handleRenameSelected()),

  // alt key usage
  alt_enter: guarded(() => handleOpenDetailsSelected()),
});

useEventListener(containerRef, "mousedown", handleMouseNavigate);

// Escape lives outside defineShortcuts on purpose: the shortcut registry
// preventDefaults on match even when our guard would reject the press, which
// blocks Reka's menu/drawer/modal dismissal (it bails on default-prevented
// keys). This listener checks eligibility first and never prevents default,
// so ineligible presses reach Reka and native behavior untouched.
const handleEscapeKey = (event: KeyboardEvent) => {
  if (event.defaultPrevented || !canHandleCommand(event)) return;
  if (dragSession.isDragging.value) return;
  // Drawers can keep focus on their trigger, outside the dialog element.
  if (
    contextMenuOpen.value ||
    activeDetailsFile.value ||
    activeDetailsDirectory.value ||
    isMobileUploadSheetOpen.value
  ) {
    return;
  }
  cancelCut();
  clearSelection();
  lastSelected.value = "";
};

useEventListener(window, "keydown", (event) => {
  if (event.key === "Escape") handleEscapeKey(event);
});

const revealFileId = tabStore.takeRevealFileId(tabId);

if (revealFileId) {
  useExplorerReveal({
    fileId: revealFileId,
    directoryId: tabStore.getTab(tabId)?.activeDirId ?? null,
    currentDirId,
    filesList,
    hasMoreFiles: () => filePagination.value.hasNext,
    loadMoreFiles,
    select: (fileId) => {
      clearSelection();
      toggleSelect(fileId, "file");
      lastSelected.value = fileId;
    },
  });
}

onMounted(() => {
  const tab = tabStore.getTab(tabId);
  navigateTo(tab?.activeDirId);
});

onUnmounted(() => {
  isDisposed.value = true;
  activeDetailsDirectory.value = null;
  fileTooltip.release();
  closeContextMenu();
  if (labelTimer) clearInterval(labelTimer);
});
</script>
