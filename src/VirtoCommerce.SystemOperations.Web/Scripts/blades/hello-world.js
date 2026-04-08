angular.module('VirtoCommerce.SystemOperations')
    .controller('VirtoCommerce.SystemOperations.helloWorldController', ['$scope', 'VirtoCommerce.SystemOperations.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'SystemOperations';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'SystemOperations.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
