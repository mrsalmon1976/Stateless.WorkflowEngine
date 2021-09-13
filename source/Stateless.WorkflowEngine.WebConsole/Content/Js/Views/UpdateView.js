
var UpdateView = function () {

    var that = this;


    this.init = function () {
        this.installUpdate();
    };

    this.installUpdate = function () {

        var request = $.ajax({
            url: "/update/install",
            method: "GET",
        });

        request.done(function (response) {
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            $('#pnl-update').removeClass('alert-info').addClass('alert-danger').html('Update failed - please try again or apply manually.');
        });
    };

}


$(document).ready(function()
{
    var uv = new UpdateView();
    uv.init();
});
