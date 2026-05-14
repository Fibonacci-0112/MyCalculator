// Minimal localStorage shim used by LocalStoragePaycheckRepository.
// All saved paychecks are stored under a single key as a JSON array, mirroring
// the MAUI JsonPaycheckRepository's single-file model so the on-disk and
// in-browser schemas can stay in sync.
window.paycheckStorage = {
    get: function (key) {
        return window.localStorage.getItem(key);
    },
    set: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    remove: function (key) {
        window.localStorage.removeItem(key);
    }
};
