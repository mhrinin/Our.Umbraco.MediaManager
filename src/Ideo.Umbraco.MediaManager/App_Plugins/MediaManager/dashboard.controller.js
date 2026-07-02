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

    var POLL_INTERVAL_MS = 1000;
    // Job state is in-memory server-side; this many consecutive missing statuses means the job
    // was lost (app restart) and polling must stop instead of retrying forever.
    var MAX_MISSING_STATUS = 5;

    function DashboardController($scope, $q, $timeout, mediaManagerResource, notificationsService, overlayService, editorService) {
        var vm = this;
        var destroyed = false;
        var pollTimeouts = {};

        vm.tabs = SCAN_TABS;
        vm.activeTab = "UnusedMedia";
        vm.scanning = false;
        vm.formatBytes = formatBytes;

        vm.slices = {};
        SCAN_TABS.forEach(function (t) {
            vm.slices[t.key] = { items: [], reclaimable: 0, state: "idle", jobId: null };
        });
        vm.slices.StorageReport = { report: null, state: "idle" };

        vm.setTab = setTab;
        vm.count = count;
        vm.reclaimable = reclaimable;
        vm.rescan = scanAll;
        vm.retryActive = function () { runScan(vm.activeTab); };
        vm.toggleAll = toggleAll;
        vm.allSelected = allSelected;
        vm.anySelected = anySelected;
        vm.deleteSelected = deleteSelected;
        vm.openMedia = openMedia;
        vm.activeSlice = function () { return vm.slices[vm.activeTab]; };
        vm.isMediaTab = function () { return vm.activeTab !== "OrphanedFiles"; };

        $scope.$on("$destroy", function () {
            destroyed = true;
            Object.keys(pollTimeouts).forEach(function (key) {
                if (pollTimeouts[key]) { $timeout.cancel(pollTimeouts[key]); }
            });
        });

        activate();

        function activate() { scanAll(); }

        function pollScan(type) {
            var deferred = $q.defer();
            mediaManagerResource.startScan(type).then(function (res) {
                var jobId = res.jobId;
                var missingStatus = 0;

                function poll() {
                    if (destroyed) { deferred.reject("destroyed"); return; }
                    mediaManagerResource.getStatus(jobId).then(function (status) {
                        missingStatus = 0;
                        var state = status && status.state ? String(status.state).toLowerCase() : "";
                        if (state === "completed") {
                            mediaManagerResource.getResult(jobId).then(deferred.resolve, deferred.reject);
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
            slice.state = "scanning";

            if (type === "StorageReport") {
                return pollScan(type).then(function (result) {
                    slice.report = result.report || null;
                    slice.state = slice.report ? "done" : "failed";
                }, function () {
                    if (!destroyed) { slice.state = "failed"; }
                });
            }

            var tab = null;
            SCAN_TABS.forEach(function (t) { if (t.key === type) { tab = t; } });
            return pollScan(type).then(function (result) {
                slice.items = (tab.isMedia ? result.media : result.files) || [];
                slice.reclaimable = result.reclaimableBytes || 0;
                slice.jobId = result.jobId;
                slice.state = "done";
            }, function () {
                if (!destroyed) {
                    slice.items = [];
                    slice.jobId = null;
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
            $q.all(types.map(runScan)).finally(function () {
                vm.scanning = false;
            });
        }

        function setTab(key) {
            vm.activeTab = key;
            // The storage report is loaded lazily, the first time its tab is opened.
            if (key === "StorageReport" && vm.slices.StorageReport.state === "idle") {
                runScan("StorageReport");
            }
        }

        function count(key) {
            var slice = vm.slices[key];
            return slice && slice.items ? slice.items.length : 0;
        }

        function reclaimable() {
            var total = 0;
            SCAN_TABS.forEach(function (t) { total += vm.slices[t.key].reclaimable || 0; });
            return formatBytes(total);
        }

        function toggleAll() {
            var slice = vm.activeSlice();
            var select = !allSelected();
            slice.items.forEach(function (i) { i.selected = select; });
        }

        function allSelected() {
            var slice = vm.activeSlice();
            return slice.items.length > 0 && slice.items.every(function (i) { return i.selected; });
        }

        function anySelected() {
            return vm.activeSlice().items.some(function (i) { return i.selected; });
        }

        function selectedIds() {
            var isMedia = vm.isMediaTab();
            return vm.activeSlice().items
                .filter(function (i) { return i.selected; })
                .map(function (i) { return isMedia ? i.key : i.path; });
        }

        function deleteSelected() {
            var ids = selectedIds();
            if (!ids.length) { return; }
            var isFiles = vm.activeTab === "OrphanedFiles";
            var jobId = vm.activeSlice().jobId;
            overlayService.confirmDelete({
                title: "Delete " + ids.length + " item(s)",
                content: isFiles
                    ? "The selected files will be permanently deleted from disk. This cannot be undone."
                    : "The selected media will be moved to the Recycle Bin, where they can be restored.",
                submitButtonLabelKey: "actions_delete",
                submit: function () {
                    overlayService.close();
                    var op = isFiles
                        ? mediaManagerResource.deleteFiles(jobId, ids, false)
                        : mediaManagerResource.deleteMedia(ids, false);
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
                id: item.key,
                submit: function () { editorService.close(); },
                close: function () { editorService.close(); }
            });
        }
    }

    DashboardController.$inject = ["$scope", "$q", "$timeout", "mediaManagerResource", "notificationsService", "overlayService", "editorService"];
    angular.module("umbraco").controller("MediaManager.DashboardController", DashboardController);
})();
