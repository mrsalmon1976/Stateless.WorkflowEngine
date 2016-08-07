
var UserView = function () {

    var that = this;

    this.errorSelector = '#user-msg-error';

    this.init = function () {
        this.loadUsers();
        $('#btn-add-user').on('click', function () { that.showForm(''); });
        $('#btn-submit-user').on('click', that.submitForm);
        $('#dlg-user').on('shown.bs.modal', function () {
            $('#txt-user').focus();
        });
    };

    this.loadUsers = function () {

        $('#pnl-loading').show();

        var request = $.ajax({
            url: "/user/list",
            method: "GET",
            dataType: 'html'
        });

        request.done(function (response) {
            //debugger;
            $('#pnl-users').html(response);
        });

        request.fail(function (xhr, textStatus, errorThrown) {
            alert('error: ' + xhr.responseText);
        });
        request.always(function (xhr, textStatus) {
            //debugger;
            $('#pnl-loading').hide();
        });
    };

    this.showError = function (error) {
        //debugger;
        var err = error;
        if ($.isArray(err)) {
            err = Collections.displayList(err);
        }
        $("#msg-error").html(err);
        $("#msg-error").removeClass('hidden');
    };

    this.showForm = function () {
        $("#msg-error").addClass('hidden');
        $('#dlg-user').modal('show');
    };

    this.submitForm = function () {
        $("#msg-error").addClass('hidden');
//        debugger;
        var formData = $('#form-user').serializeForm();
        var request = $.ajax({
            url: "/user/save",
            method: "POST",
            data: formData,
            dataType: 'json',
            traditional: true
        });

        request.done(function (response) {
            if (response.success === false) {
                Utils.showError(that.errorSelector, response.messages[0]);
            }
            else {
                $('#dlg-user').modal('hide');
                that.loadUsers();
                $('#form-user')[0].reset();
            }
        });

        request.fail(function (xhr, textStatus) {
            try {
                Utils.showError(that.errorSelector, xhr.responseJSON.message);
            }
            catch (err) {
                Utils.showError(that.errorSelector, 'A fatal error occurred');
            }
        });
    };

}


$(document).ready(function()
{
    var uv = new UserView();
    uv.init();
});
