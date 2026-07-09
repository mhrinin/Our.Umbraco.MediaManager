(function () {
    "use strict";

    var UNITS = ["B", "KB", "MB", "GB", "TB"];
    function formatBytes(bytes) {
        if (!bytes || bytes <= 0) { return "0 B"; }
        var exp = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), UNITS.length - 1);
        return (bytes / Math.pow(1024, exp)).toFixed(exp === 0 ? 0 : 1) + " " + UNITS[exp];
    }

    var SCAN_TABS = [
        { key: "UnusedMedia", label: "Unused media", icon: "icon-picture", desc: "Media not referenced by any content.", isMedia: true },
        { key: "Duplicates", label: "Duplicates", icon: "icon-documents", desc: "Redundant copies of identical files.", isMedia: true },
        { key: "BrokenMedia", label: "Broken media", icon: "icon-alert", desc: "Media whose file is missing on disk.", isMedia: true },
        { key: "OrphanedFiles", label: "Orphaned files", icon: "icon-document", desc: "Files on disk with no matching media item.", isMedia: false }
    ];

    var PAGE_SIZE = 50;
    var POLL_INTERVAL_MS = 1000;
    // Job state is in-memory server-side; this many consecutive missing statuses means the job
    // was lost (app restart) and polling must stop instead of retrying forever.
    var MAX_MISSING_STATUS = 5;

    var IMAGE_EXTENSIONS = { jpg: 1, jpeg: 1, png: 1, gif: 1, webp: 1, avif: 1, svg: 1, bmp: 1, tiff: 1 };

    function DashboardController($scope, $q, $timeout, mediaManagerResource, notificationsService, overlayService, editorService) {
        var vm = this;
        var destroyed = false;
        var pollTimeouts = {};

        vm.tabs = SCAN_TABS;
        vm.activeTab = "UnusedMedia";
        vm.scanning = false;
        vm.reclaimableBytes = 0;
        vm.formatBytes = formatBytes;

        vm.slices = {};
        SCAN_TABS.forEach(function (t) { vm.slices[t.key] = emptySlice(); });
        vm.slices.StorageReport = emptySlice();
        vm.slices.Export = emptySlice();

        vm.setTab = setTab;
        vm.count = count;
        vm.rescan = scanAll;
        vm.retryActive = function () { runScan(vm.activeTab); };
        vm.activeSlice = function () { return vm.slices[vm.activeTab]; };
        vm.isMediaTab = function () { return vm.activeTab !== "OrphanedFiles"; };
        // Unused media and duplicates exist as viewable media — show them as preview cards.
        // Broken media has no file to preview and orphaned files are not media: they keep the table.
        vm.isGridTab = function () { return vm.activeTab === "UnusedMedia" || vm.activeTab === "Duplicates"; };
        vm.pageCount = pageCount;
        vm.onPageChange = onPageChange;
        vm.isSelected = isSelected;
        vm.toggleItem = toggleItem;
        vm.togglePage = togglePage;
        vm.pageAllChecked = pageAllChecked;
        vm.selectedCount = selectedCount;
        vm.selectAllTotal = selectAllTotal;
        vm.clearSelection = clearSelection;
        vm.deleteSelected = deleteSelected;
        vm.openMedia = openMedia;
        vm.thumbnailUrl = thumbnailUrl;
        vm.createExport = function () { runScan("Export"); };
        vm.downloadExport = downloadExport;

        $scope.$on("$destroy", function () {
            destroyed = true;
            Object.keys(pollTimeouts).forEach(function (key) {
                if (pollTimeouts[key]) { $timeout.cancel(pollTimeouts[key]); }
            });
        });

        activate();

        function activate() { scanAll(); }

        function emptySlice() {
            return { state: "idle", processed: 0, summary: null, page: 1, pageItems: [], selectedIds: {}, allSelected: false };
        }

        function isCleanupTab(type) {
            return SCAN_TABS.some(function (t) { return t.key === type; });
        }

        function pollScan(type, slice) {
            var deferred = $q.defer();
            mediaManagerResource.startScan(type).then(function (res) {
                var jobId = res.jobId;
                var missingStatus = 0;

                function poll() {
                    if (destroyed) { deferred.reject("destroyed"); return; }
                    mediaManagerResource.getStatus(jobId).then(function (status) {
                        missingStatus = 0;
                        slice.processed = (status && status.processed) || 0;
                        var state = status && status.state ? String(status.state).toLowerCase() : "";
                        if (state === "completed") {
                            deferred.resolve(jobId);
                        } else if (state === "failed" || state === "cancelled") {
                            deferred.reject(status);
                        } else {
                            schedule();
                        }
                    }, function () {
                        // Missing job (404 after app restart) or transient error: cap the retries.
                        if (++missingStatus >= MAX_MISSING_STATUS) {
                            deferred.reject("lost");
                        } else {
                            schedule();
                        }
                    });
                }

                function schedule() {
                    if (destroyed) { deferred.reject("destroyed"); return; }
                    pollTimeouts[type] = $timeout(poll, POLL_INTERVAL_MS);
                }

                schedule();
            }, deferred.reject);
            return deferred.promise;
        }

        function runScan(type) {
            var slice = vm.slices[type];
            if (slice.state === "scanning") { return $q.resolve(); }
            angular.extend(slice, emptySlice());
            slice.state = "scanning";

            return pollScan(type, slice).then(function (jobId) {
                return mediaManagerResource.getResult(jobId).then(function (summary) {
                    slice.summary = summary;
                    if (!isCleanupTab(type)) {
                        slice.state = "done";
                        return;
                    }
                    return mediaManagerResource.getResultItems(jobId, 0, PAGE_SIZE).then(function (pageResult) {
                        slice.pageItems = pageResult.items || [];
                        slice.page = 1;
                        slice.state = "done";
                        refreshReclaimable();
                    });
                });
            }).catch(function () {
                if (!destroyed) {
                    angular.extend(slice, emptySlice());
                    slice.state = "failed";
                }
            });
        }

        function scanAll() {
            vm.scanning = true;
            var types = SCAN_TABS.map(function (t) { return t.key; });
            // Refresh the storage report too once it has been loaded, so it never shows stale totals.
            if (vm.slices.StorageReport.state !== "idle") {
                types.push("StorageReport");
            }
            // The export is never auto-run: it is expensive and only built on explicit request.
            $q.all(types.map(runScan)).finally(function () {
                vm.scanning = false;
            });
        }

        function refreshReclaimable() {
            // An item can be unused AND a duplicate; the server counts its size once.
            mediaManagerResource.getReclaimableBytes().then(function (res) {
                vm.reclaimableBytes = (res && res.reclaimableBytes) || 0;
            }, angular.noop);
        }

        function setTab(key) {
            vm.activeTab = key;
            // The storage report is loaded lazily, the first time its tab is opened.
            if (key === "StorageReport" && vm.slices.StorageReport.state === "idle") {
                runScan("StorageReport");
            }
            // The finished export outlives page loads on the server; restore it instead of making
            // the user rebuild a possibly huge zip that is still available.
            if (key === "Export" && vm.slices.Export.state === "idle") {
                restoreExport();
            }
        }

        function restoreExport() {
            mediaManagerResource.getLatestResult("Export").then(function (summary) {
                // Re-check: the user may have started a new export while this was in flight.
                if (summary && summary.export && vm.slices.Export.state === "idle") {
                    vm.slices.Export.summary = summary;
                    vm.slices.Export.state = "done";
                }
            }, angular.noop);
        }

        function count(key) {
            var slice = vm.slices[key];
            return slice && slice.summary ? slice.summary.totalItems : 0;
        }

        function pageCount() {
            return Math.max(1, Math.ceil(count(vm.activeTab) / PAGE_SIZE));
        }

        function onPageChange(pageNumber) {
            var slice = vm.activeSlice();
            if (!slice.summary || slice.state !== "done") { return; }
            mediaManagerResource.getResultItems(slice.summary.jobId, (pageNumber - 1) * PAGE_SIZE, PAGE_SIZE)
                .then(function (pageResult) {
                    slice.page = pageNumber;
                    slice.pageItems = pageResult.items || [];
                }, function () {
                    notificationsService.error("Media Manager", "The scan result is no longer available. Please rescan.");
                });
        }

        function isSelected(item) {
            var slice = vm.activeSlice();
            return slice.allSelected || !!slice.selectedIds[item.id];
        }

        function toggleItem(item) {
            var slice = vm.activeSlice();
            if (slice.allSelected) {
                // Any manual change drops "all selected" back to an explicit page selection.
                slice.allSelected = false;
                slice.selectedIds = {};
                slice.pageItems.forEach(function (i) {
                    if (i.id !== item.id) { slice.selectedIds[i.id] = true; }
                });
                return;
            }
            if (slice.selectedIds[item.id]) {
                delete slice.selectedIds[item.id];
            } else {
                slice.selectedIds[item.id] = true;
            }
        }

        function togglePage() {
            var slice = vm.activeSlice();
            if (slice.allSelected) {
                slice.allSelected = false;
                slice.selectedIds = {};
                return;
            }
            var allPageSelected = slice.pageItems.length > 0 && slice.pageItems.every(function (i) { return slice.selectedIds[i.id]; });
            slice.pageItems.forEach(function (i) {
                if (allPageSelected) { delete slice.selectedIds[i.id]; } else { slice.selectedIds[i.id] = true; }
            });
        }

        function pageAllChecked() {
            var slice = vm.activeSlice();
            return slice.allSelected
                || (slice.pageItems.length > 0 && slice.pageItems.every(function (i) { return slice.selectedIds[i.id]; }));
        }

        function selectedCount() {
            var slice = vm.activeSlice();
            return slice.allSelected ? count(vm.activeTab) : Object.keys(slice.selectedIds).length;
        }

        function selectAllTotal() {
            var slice = vm.activeSlice();
            slice.allSelected = true;
            slice.selectedIds = {};
        }

        function clearSelection() {
            var slice = vm.activeSlice();
            slice.allSelected = false;
            slice.selectedIds = {};
        }

        function deleteSelected() {
            var slice = vm.activeSlice();
            var total = selectedCount();
            if (!total || !slice.summary) { return; }
            var isFiles = vm.activeTab === "OrphanedFiles";
            var jobId = slice.summary.jobId;
            overlayService.confirmDelete({
                title: "Delete " + total + " item(s)",
                content: isFiles
                    ? "The selected files will be permanently deleted from disk. This cannot be undone."
                    : "The selected media will be moved to the Recycle Bin, where they can be restored.",
                submitButtonLabelKey: "actions_delete",
                submit: function () {
                    overlayService.close();
                    // With allSelected the server resolves the full target list from the scan
                    // result; otherwise only the explicitly selected ids are sent.
                    var op = slice.allSelected
                        ? mediaManagerResource.deleteAll(jobId, false)
                        : mediaManagerResource.deleteItems(jobId, Object.keys(slice.selectedIds), false);
                    op.then(function (result) {
                        var affected = (result && result.affected) || 0;
                        var errors = (result && result.errors) || [];
                        if (errors.length) {
                            notificationsService.warning("Media Manager", affected + " processed, " + errors.length + " error(s).");
                        } else {
                            notificationsService.success("Media Manager", affected + " item(s) processed.");
                        }
                        scanAll();
                    }, function () {
                        notificationsService.error("Media Manager", "Delete failed.");
                    });
                },
                close: function () { overlayService.close(); }
            });
        }

        function openMedia(item) {
            editorService.mediaEditor({
                id: item.id,
                submit: function () { editorService.close(); },
                close: function () { editorService.close(); }
            });
        }

        function thumbnailUrl(item) {
            if (!item.path) { return null; }
            var extension = item.path.split(".").pop().toLowerCase();
            return IMAGE_EXTENSIONS[extension] ? item.path + "?rmode=min&width=300&height=300" : null;
        }

        function downloadExport() {
            var summary = vm.slices.Export.summary;
            if (!summary || !summary.export) { return; }
            // Assigning location issues a real request; the attachment response means the browser
            // starts the download and never leaves the page.
            window.location.href = mediaManagerResource.exportDownloadUrl(summary.jobId, summary.export.downloadToken);
        }
    }

    DashboardController.$inject = ["$scope", "$q", "$timeout", "mediaManagerResource", "notificationsService", "overlayService", "editorService"];
    angular.module("umbraco").controller("MediaManager.DashboardController", DashboardController);
})();
