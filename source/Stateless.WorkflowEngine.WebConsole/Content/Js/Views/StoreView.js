
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

    //    this.showError = function (error) {
    //        //debugger;
    //        var err = error;
    //        if ($.isArray(err)) {
    //            err = Collections.displayList(err);
    //        }
    //        $("#msg-error").html(err);
    //        $("#msg-error").removeClass('hidden');
    //    };

    //    this.showForm = function () {
    //        $("#msg-error").addClass('hidden');
    //        $('#dlg-add').modal('show');
    //    };

    //    this.submitForm = function () {
    //        $("#msg-error").addClass('hidden');
    ////        debugger;
    //        var formData = $('#form-user').serializeForm();
    //        var request = $.ajax({
    //            url: "/user",
    //            method: "POST",
    //            data: formData,
    //            dataType: 'json',
    //            traditional: true
    //        });

    //        request.done(function (response) {
    //            //debugger;
    //            if (response.success) {
    //                $('#dlg-add').modal('hide');
    //                that.loadUsers();
    //            }
    //            else {
    //                that.showError(response.messages);
    //            }
    //        });

    //        request.fail(function (xhr, textStatus) {
    //            try {
    //                that.showError(xhr.responseJSON.message);
    //            }
    //            catch (err) {
    //                that.showError('A fatal error occurred');
    //            }
    //        });
    //    };

};

$(document).ready(function()
{
    new StoreView().init();
});
