
var StoreView = function () {

    var that = this;

    this.init = function () {
        $('#btn-refresh').on('click', function () { that.loadWorkflows(); });
        this.loadWorkflows();
    };

    this.loadWorkflows = function () {

        var connId = $('#pnl-workflows').data().modelId;
        $('a.workflow-id').off('click');    // remove row event handlers
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
            // attach row event handlers again
            $('a.workflow-id').on('click', function () { that.openWorkflowDialog($(this)); });
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            var html = Utils.createErrorAlert('ERROR: ' + xhr.responseText);
            $('#pnl-workflows').html(html);
        });
        request.always(function (xhr, textStatus) {
            $('#pnl-loading').hide();
        });
    };

    this.openWorkflowDialog = function (evtSource) {
        var workflowId = evtSource.data('id');
        var connId = $('#pnl-workflows').data().modelId;
        $('#txt-workflow-json').val('');
        $('#dlg-workflow').modal('show');
        $('#workflow-spinner').show();
        $('#workflow-msg-error').hide();

        var request = $.ajax({
            url: "/store/workflow",
            method: "POST",
            dataType: 'html',
            data: { "WorkflowId": workflowId, "ConnectionId" : connId }
        });

        request.done(function (response) {
            $('#txt-workflow-json').val(response);
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            //var html = Utils.createErrorAlert('ERROR: ' + xhr.responseText);
            $('#workflow-msg-error').show();
        });
        request.always(function (xhr, textStatus) {
            $('#workflow-spinner').hide();
        });
    }
};

$(document).ready(function()
{
    new StoreView().init();
});
