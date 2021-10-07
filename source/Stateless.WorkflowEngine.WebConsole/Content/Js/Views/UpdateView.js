
var UpdateView = function () {

    var that = this;


    this.init = function () {
        window.setInterval(() => {
            that.checkForUpdates()
        }, 10000);
    };

    this.checkForUpdates = function () {

        var request = $.ajax({
            url: "/dashboard/checkforupdate",
            method: "GET",
            dataType: 'json'
        });

        request.done(function (response) {
            window.location.href = '/';
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            // do nothing...we just keep trying
        });
    };

}


$(document).ready(function()
{
    var uv = new UpdateView();
    uv.init();
});
