
var StoreView = function () {

    var that = this;

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadWorkflows(); });
        this.loadWorkflows();
    };

    this.loadWorkflows = function () {

        var connId = $('#pnl-workflows').data().modelId;
        $('#pnl-loading').show();
        $('#pnl-workflows').html('');

        var request = $.ajax({
            url: "/store/list",
            method: "POST",
            dataType: 'html',
            data: { "id": connId }
        });

        request.done(function (response) {
            $('#pnl-workflows').html(response);
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            var html = Utils.createErrorAlert('ERROR: ' + xhr.responseText);
            $('#pnl-workflows').html(html);
        });
        request.always(function (xhr, textStatus) {
            $('#pnl-loading').hide();
        });
    };

};

$(document).ready(function()
{
    new StoreView().init();
});
