angular.module('VirtoCommerce.SystemOperations')
    .factory('VirtoCommerce.SystemOperations.webApi', ['$resource', function ($resource) {
        return $resource('api/system-operations');
    }]);
