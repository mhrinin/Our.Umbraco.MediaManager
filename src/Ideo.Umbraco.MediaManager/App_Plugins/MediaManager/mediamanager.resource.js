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
            deleteMedia: function (keys, dryRun) {
                return handle($http.post(base + "DeleteMedia", { keys: keys, dryRun: dryRun }), "Failed to delete media");
            },
            deleteFiles: function (jobId, paths, dryRun) {
                return handle($http.post(base + "DeleteFiles", { jobId: jobId, paths: paths, dryRun: dryRun }), "Failed to delete files");
            }
        };
    }

    mediaManagerResource.$inject = ["$http", "umbRequestHelper"];
    angular.module("umbraco.resources").factory("mediaManagerResource", mediaManagerResource);
})();
