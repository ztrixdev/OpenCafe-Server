window.blazorCulture = {
    set: function (value) {
        document.cookie = `.AspNetCore.Culture=c=${value}|uic=${value}; path=/`;
    },
    get: function () {
        const match = document.cookie.match(/\.AspNetCore\.Culture=c=([^|]+)\|uic=([^;]+)/);
        return match ? match[1] : null;
    }
};

window.userCredentials = {
    setToken: function (value) {
        const date = new Date();
        date.setTime(date.getTime() + (12 * 60 * 60 * 1000)); // 12 hrs
        let expireDate = "expires=" + date.toUTCString();

        document.cookie = `token=${value}; ${expireDate}; path=/`;
    },
    getToken: function () {
        const match = document.cookie.match(/(?:^|;\s*)token=([^;]*)/);
        return match ? match[1] : null;
    },
    removeToken: function () {
        document.cookie = "token=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/";
    },

    setInstance: function (value) {
        const date = new Date();
        date.setTime(date.getTime() + (2 * 24 * 60 * 60 * 1000)); // 2 days
        let expireDate = "expires=" + date.toUTCString();

        document.cookie = `instance=${value}; ${expireDate}; path=/`;
    },
    getInstance: function () {
        const match = document.cookie.match(/(?:^|;\s*)instance=([^;]*)/);
        return match ? match[1] : null;
    },
    removeInstance: function () {
        document.cookie = "instance=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/";
    }
}
