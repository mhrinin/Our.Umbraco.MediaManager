(function () {
    "use strict";

    // The legacy backoffice API serializes with PascalCase property names; the dashboard works in
    // camelCase, so normalise every response here.
    function camelize(value) {
        if (Array.isArray(value)) {
            return value.map(camelize);
        }
        if (value && typeof value === "object") {
            var out = {};
            Object.keys(value).forEach(function (key) {
                out[key.charAt(0).toLowerCase() + key.slice(1)] = camelize(value[key]);
            });
            return out;
        }
        return value;
    }

    function mediaManagerResource($http, umbRequestHelper) {
        var base = "/umbraco/backoffice/MediaManager/MediaManager/";

        function handle(promise, message) {
            return umbRequestHelper.resourcePromise(promise, message).then(camelize);
        }

        return {
            startScan: function (type) {
                return handle($http.post(base + "StartScan?type=" + encodeURIComponent(type)), "Failed to start scan");
            },
            getStatus: function (jobId) {
                return handle($http.get(base + "GetStatus?jobId=" + jobId), "Failed to get scan status");
            },
            getResult: function (jobId) {
                return handle($http.get(base + "GetResult?jobId=" + jobId), "Failed to get scan result");
            },
            getResultItems: function (jobId, skip, take) {
                return handle(
                    $http.get(base + "GetResultItems?jobId=" + jobId + "&skip=" + skip + "&take=" + take),
                    "Failed to get scan result items");
            },
            getLatestResult: function (type) {
                return handle($http.get(base + "GetLatestResult?type=" + encodeURIComponent(type)), "Failed to get latest result");
            },
            getReclaimableBytes: function () {
                return handle($http.get(base + "GetReclaimableBytes"), "Failed to get reclaimable space");
            },
            deleteItems: function (jobId, ids, dryRun) {
                return handle($http.post(base + "DeleteItems?jobId=" + jobId, { ids: ids, dryRun: dryRun }), "Failed to delete items");
            },
            deleteAll: function (jobId, dryRun) {
                return handle($http.post(base + "DeleteAll?jobId=" + jobId, { dryRun: dryRun }), "Failed to delete items");
            },
            // The token IS the credential (the download endpoint is anonymous but validates
            // jobId + token), which is what lets the browser download a multi-GB zip natively.
            exportDownloadUrl: function (jobId, token) {
                return "/Umbraco/Api/MediaManagerExport/Download?jobId=" + jobId + "&token=" + encodeURIComponent(token);
            }
        };
    }

    mediaManagerResource.$inject = ["$http", "umbRequestHelper"];
    angular.module("umbraco.resources").factory("mediaManagerResource", mediaManagerResource);
})();
