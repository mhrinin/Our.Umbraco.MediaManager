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

    function DashboardController($scope, $q, $timeout, mediaManagerResource, notificationsService, overlayService, editorService) {
        var vm = this;

        vm.tabs = SCAN_TABS;
        vm.activeTab = "UnusedMedia";
        vm.scanning = false;
        vm.report = null;
        vm.reportLoading = false;
        vm.formatBytes = formatBytes;

        vm.slices = {};
        SCAN_TABS.forEach(function (t) {
            vm.slices[t.key] = { items: [], reclaimable: 0, state: "idle" };
        });

        vm.setTab = setTab;
        vm.count = count;
        vm.reclaimable = reclaimable;
        vm.rescan = scanAll;
        vm.toggleAll = toggleAll;
        vm.allSelected = allSelected;
        vm.anySelected = anySelected;
        vm.deleteSelected = deleteSelected;
        vm.openMedia = openMedia;
        vm.activeSlice = function () { return vm.slices[vm.activeTab]; };
        vm.isMediaTab = function () { return vm.activeTab !== "OrphanedFiles"; };
        vm.rowId = function (item) { return vm.isMediaTab() ? item.key : item.path; };

        activate();

        function activate() { scanAll(); }

        function pollScan(type) {
            var deferred = $q.defer();
            mediaManagerResource.startScan(type).then(function (res) {
                var jobId = res.jobId;
                function poll() {
                    mediaManagerResource.getStatus(jobId).then(function (status) {
                        var state = (status && status.state ? String(status.state) : "").toLowerCase();
                        if (state === "completed") {
                            mediaManagerResource.getResult(jobId).then(deferred.resolve, deferred.reject);
                        } else if (state === "failed" || state === "cancelled") {
                            deferred.reject(status);
                        } else {
                            $timeout(poll, 1000);
                        }
                    }, function () { $timeout(poll, 1000); });
                }
                poll();
            }, deferred.reject);
            return deferred.promise;
        }

        function scanAll() {
            vm.scanning = true;
            var promises = SCAN_TABS.map(function (t) {
                vm.slices[t.key].state = "scanning";
                return pollScan(t.key).then(function (result) {
                    vm.slices[t.key].items = (t.isMedia ? result.media : result.files) || [];
                    vm.slices[t.key].reclaimable = result.reclaimableBytes || 0;
                    vm.slices[t.key].state = "done";
                }, function () {
                    vm.slices[t.key].items = [];
                    vm.slices[t.key].state = "failed";
                });
            });
            $q.all(promises).finally(function () {
                vm.scanning = false;
                if (vm.report || vm.activeTab === "StorageReport") { loadReport(true); }
            });
        }

        function setTab(key) {
            vm.activeTab = key;
            if (key === "StorageReport" && !vm.report && !vm.reportLoading) { loadReport(false); }
        }

        function loadReport(force) {
            if (vm.reportLoading) { return; }
            if (vm.report && !force) { return; }
            vm.reportLoading = true;
            mediaManagerResource.storageReport().then(function (report) {
                vm.report = report;
            }).finally(function () { vm.reportLoading = false; });
        }

        function count(key) {
            var slice = vm.slices[key];
            return slice ? slice.items.length : 0;
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
            overlayService.confirmDelete({
                title: "Delete " + ids.length + " item(s)",
                content: isFiles
                    ? "The selected files will be permanently deleted from disk. This cannot be undone."
                    : "The selected media will be moved to the Recycle Bin, where they can be restored.",
                submitButtonLabelKey: "actions_delete",
                submit: function () {
                    overlayService.close();
                    var op = isFiles
                        ? mediaManagerResource.deleteFiles(ids, false)
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
